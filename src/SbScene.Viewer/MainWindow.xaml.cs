using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SbScene.Core.Rendering;
using SbScene.Core.Semantics;

namespace SbScene.Viewer;

public partial class MainWindow : Window
{
    private const double PlaybackFramesPerSecond = 60.0;

    private SbSceneFile? _scene;
    private SceneRenderBuildCache? _renderBuildCache;
    private SvoRenderResources? _resources;
    private RenderScene? _renderScene;
    private string? _scenePath;
    private string? _svoPath;
    private string? _pendingSvoPath;
    private bool _controlsReady;
    private readonly HashSet<int> _hiddenNodeIndexes = [];
    private readonly HashSet<int> _shownNodeIndexes = [];
    private HashSet<int>? _selectedNodeIndexes;
    private int? _selectedNodeIndex;
    private readonly List<AnimationListItem> _animationItems = [];
    private readonly List<AnimationPlaybackSlot> _animationSlots = [];
    private bool _isUpdatingAnimationControls;
    private int? _selectedAnimationIndex;
    private int? _previewAnimationIndex;
    private bool _isPlaybackRenderingHooked;
    private TimeSpan? _playbackRenderingStartTime;
    private double _playbackStartFrame;
    private double _currentFrame;
    private double _endFrame;
    private bool _isPlaying;
    private bool _isLooping;

    public MainWindow()
    {
        InitializeComponent();
        _controlsReady = true;
        SetZoom(1);
        UpdateAnimationControls();
        UpdateSelectedNodeInfo();
        SetStatus("就绪。");
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var scenePath = args.FirstOrDefault(IsSceneFile);
        var svoPath = args.FirstOrDefault(IsSvoFile);

        if (scenePath is not null)
        {
            await LoadSceneAsync(scenePath, svoPath);
        }
        else if (svoPath is not null)
        {
            _pendingSvoPath = svoPath;
            SvoSummaryTextBlock.Text = $"预选 SVO：{Path.GetFileName(svoPath)}";
            SetStatus("已记录 SVO，打开 sbscene 后会尝试绑定。");
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        DetachPlaybackRendering();
    }

    private async void OpenScene_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "打开 sbscene",
            Filter = "SbScene files (*.sbscene)|*.sbscene|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            await LoadSceneAsync(dialog.FileName, null);
        }
    }

    private async void OpenSvo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "绑定 SVO",
            Filter = "SVO files (*.svo)|*.svo|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            await LoadSvoAsync(dialog.FileName);
        }
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_scenePath is null)
        {
            SetStatus("还没有加载 sbscene。");
            return;
        }

        await LoadSceneAsync(_scenePath, _svoPath);
    }

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        FitSceneToViewport();
    }

    private void RenderOption_Changed(object sender, RoutedEventArgs e)
    {
        if (!_controlsReady)
        {
            return;
        }

        RebuildRender();
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_controlsReady || RenderSurface is null)
        {
            return;
        }

        SetZoom(e.NewValue, updateSlider: false);
    }

    private void AnimationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_controlsReady || _isUpdatingAnimationControls)
        {
            return;
        }

        if (AnimationComboBox.SelectedItem is not AnimationListItem item)
        {
            SelectAnimation(null, rebuild: true, activatePreview: true);
            return;
        }

        SelectAnimation(item.Index, rebuild: true, activatePreview: true);
    }

    private void AnimationFrameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_controlsReady || _isUpdatingAnimationControls || GetSelectedAnimation() is null)
        {
            return;
        }

        SeekSelectedAnimationFrame(
            e.NewValue,
            pauseNonStaticPlayback: true,
            updateSlider: false,
            updateDetails: true);
    }

    private void PlayAnimation_Click(object sender, RoutedEventArgs e)
    {
        StartPlayback();
    }

    private void PauseAnimation_Click(object sender, RoutedEventArgs e)
    {
        PausePlayback();
        RebuildRender(fitSelectionPreview: false);
    }

    private void StopAnimation_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
    }

    private void ResetAnimationStates_Click(object sender, RoutedEventArgs e)
    {
        PausePlayback(updateControls: false);
        ResetAnimationSlots();
        _currentFrame = 0;
        SyncSelectedAnimationFieldsFromSlot();
        UpdateAnimationControls();
        RebuildRender(fitSelectionPreview: false);
        SetStatus("已重置所有动画状态。");
    }

    private void LockAnimationSlot_Changed(object sender, RoutedEventArgs e)
    {
        if (!_controlsReady || _isUpdatingAnimationControls)
        {
            return;
        }

        SetSelectedAnimationSlotLocked(LockAnimationSlotCheckBox.IsChecked == true);
        UpdateAnimationControls();
        RebuildRender(fitSelectionPreview: false);
    }

    private void LoopAnimation_Changed(object sender, RoutedEventArgs e)
    {
        if (!_controlsReady || _isUpdatingAnimationControls)
        {
            return;
        }

        _isLooping = LoopAnimationCheckBox.IsChecked == true;
        UpdateSelectedAnimationSlot();
        UpdateAnimationControls();
    }

    private void SelectionPreviewBounds_Changed(object sender, RoutedEventArgs e)
    {
        if (!_controlsReady || SelectionRenderSurface is null || SelectionPreviewBoundsCheckBox is null)
        {
            return;
        }

        SelectionRenderSurface.ShowSelectionBounds = SelectionPreviewBoundsCheckBox.IsChecked == true;
    }

    private void NodeTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (NodeTree.SelectedItem is NodeTreeItem row)
        {
            HighlightNodeSubtree(row);
        }
        else
        {
            _selectedNodeIndex = null;
            _selectedNodeIndexes = null;
            UpdateRenderSurfaceScene(fitSelectionPreview: false);
            UpdateSelectedNodeInfo();
        }
    }

    private void NodeTreeItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (item is null)
        {
            return;
        }

        item.Focus();
        item.IsSelected = true;
        e.Handled = true;
    }

    private void ShowSubtree_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedSubtreeVisibility(true);
    }

    private void HideSubtree_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedSubtreeVisibility(false);
    }

    private void ResetSubtreeVisibility_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedSubtreeVisibility(null);
    }

    private void RenderSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var nodeIndex = RenderSurface.HitTestNode(e.GetPosition(RenderSurface));
        if (nodeIndex is null)
        {
            return;
        }

        SelectNode(nodeIndex.Value);
        e.Handled = true;
    }

    private void SceneScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        var oldZoom = RenderSurface.Zoom;
        var mouseOnSurface = e.GetPosition(RenderSurface);
        var mouseOnViewer = e.GetPosition(SceneScrollViewer);
        var zoomFactor = e.Delta > 0 ? 1.12 : 1 / 1.12;
        SetZoom(oldZoom * zoomFactor);
        SceneScrollViewer.UpdateLayout();

        var newZoom = RenderSurface.Zoom;
        SceneScrollViewer.ScrollToHorizontalOffset(mouseOnSurface.X / oldZoom * newZoom - mouseOnViewer.X);
        SceneScrollViewer.ScrollToVerticalOffset(mouseOnSurface.Y / oldZoom * newZoom - mouseOnViewer.Y);
        e.Handled = true;
    }

    private void SceneScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateRenderSurfaceMinimumWidth();
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = ((string[])e.Data.GetData(DataFormats.FileDrop))
            .Where(File.Exists)
            .ToArray();
        var scenePath = files.FirstOrDefault(IsSceneFile);
        var svoPath = files.FirstOrDefault(IsSvoFile);

        if (scenePath is not null)
        {
            await LoadSceneAsync(scenePath, svoPath);
        }
        else if (svoPath is not null)
        {
            await LoadSvoAsync(svoPath);
        }
    }

    private async Task LoadSceneAsync(string path, string? explicitSvoPath)
    {
        if (!File.Exists(path))
        {
            SetStatus($"找不到 sbscene：{path}");
            return;
        }

        using var busy = BeginBusy($"正在加载 {Path.GetFileName(path)}...");
        try
        {
            var scene = await Task.Run(() => new SbSceneParser().ParseFile(path));
            _scene = scene;
            _renderBuildCache = new SceneRenderBuildCache(scene);
            _scenePath = path;
            _resources = null;
            _svoPath = null;
            _selectedNodeIndex = null;
            _selectedNodeIndexes = null;
            RenderSurface.ClearViewportBounds();
            SelectionRenderSurface.ClearViewportBounds();

            var svoPath = explicitSvoPath ?? _pendingSvoPath ?? FindSiblingSvo(path);
            _pendingSvoPath = null;
            if (svoPath is not null)
            {
                try
                {
                    _resources = await Task.Run(() => SvoRenderResources.Load(scene, svoPath));
                    _svoPath = svoPath;
                }
                catch (Exception ex)
                {
                    SvoSummaryTextBlock.Text = "SVO 绑定失败。";
                    SetStatus($"sbscene 已加载，但 SVO 解析失败：{ex.Message}");
                }
            }

            _hiddenNodeIndexes.Clear();
            _shownNodeIndexes.Clear();
            RefreshAnimationList();
            RefreshNodeTree();
            UpdateSceneSummary();
            RebuildRender();
            FitSceneToViewport();
        }
        catch (Exception ex)
        {
            ClearScene();
            MessageBox.Show(this, ex.Message, "加载 sbscene 失败", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus($"加载失败：{ex.Message}");
        }
    }

    private async Task LoadSvoAsync(string path)
    {
        if (!File.Exists(path))
        {
            SetStatus($"找不到 SVO：{path}");
            return;
        }

        if (_scene is null)
        {
            _pendingSvoPath = path;
            SvoSummaryTextBlock.Text = $"预选 SVO：{Path.GetFileName(path)}";
            SetStatus("已记录 SVO，打开 sbscene 后会尝试绑定。");
            return;
        }

        _svoPath = path;
        using var busy = BeginBusy($"正在解码 {Path.GetFileName(path)}...");
        try
        {
            var scene = _scene;
            _resources = await Task.Run(() => SvoRenderResources.Load(scene, path));
            UpdateSceneSummary();
            RebuildRender();
        }
        catch (Exception ex)
        {
            _resources = null;
            MessageBox.Show(this, ex.Message, "绑定 SVO 失败", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateSceneSummary();
            RebuildRender();
            SetStatus($"SVO 绑定失败：{ex.Message}");
        }
    }

    private void PlaybackRendering(object? sender, EventArgs e)
    {
        if (!_isPlaying || _scene is null || _animationSlots.Count == 0)
        {
            PausePlayback();
            return;
        }

        if (e is not RenderingEventArgs renderingArgs)
        {
            return;
        }

        var renderingTime = renderingArgs.RenderingTime;
        _playbackRenderingStartTime ??= renderingTime;
        var elapsedSeconds = (renderingTime - _playbackRenderingStartTime.Value).TotalSeconds;
        if (elapsedSeconds <= 0)
        {
            return;
        }

        var timelineFrame = _playbackStartFrame + elapsedSeconds * PlaybackFramesPerSecond;
        var shouldContinue = SeekSelectedPlaybackFrame(timelineFrame);
        if (!shouldContinue)
        {
            PausePlayback();
        }
    }

    private void StartPlayback()
    {
        if (GetSelectedAnimation() is null || _selectedAnimationIndex is not int index)
        {
            return;
        }

        ActivateSelectedAnimationPreview();
        if (_endFrame <= 0)
        {
            _currentFrame = 0;
            UpdateSelectedAnimationSlot();
            PausePlayback(updateControls: false);
            RebuildRender(fitSelectionPreview: false);
            UpdateAnimationControls();
            return;
        }

        if (!IsStaticSelectorSlot(index) && _currentFrame >= _endFrame)
        {
            _currentFrame = 0;
        }

        UpdateSelectedAnimationSlot();
        _isPlaying = true;
        _playbackStartFrame = _currentFrame;
        _playbackRenderingStartTime = null;
        AttachPlaybackRendering();
        UpdateAnimationControls();
    }

    private void PausePlayback(bool updateControls = true)
    {
        _isPlaying = false;
        DetachPlaybackRendering();
        _playbackRenderingStartTime = null;
        if (updateControls)
        {
            UpdateAnimationControls();
        }
    }

    private void AttachPlaybackRendering()
    {
        if (_isPlaybackRenderingHooked)
        {
            return;
        }

        CompositionTarget.Rendering += PlaybackRendering;
        _isPlaybackRenderingHooked = true;
    }

    private void DetachPlaybackRendering()
    {
        if (!_isPlaybackRenderingHooked)
        {
            return;
        }

        CompositionTarget.Rendering -= PlaybackRendering;
        _isPlaybackRenderingHooked = false;
    }

    private bool SeekSelectedPlaybackFrame(double timelineFrame)
    {
        if (GetSelectedAnimation() is null || _selectedAnimationIndex is not int index)
        {
            return false;
        }

        if (IsStaticSelectorSlot(index))
        {
            return false;
        }

        if (_endFrame <= 0)
        {
            SeekSelectedAnimationFrame(0, pauseNonStaticPlayback: false, updateSlider: true, updateDetails: false);
            return false;
        }

        var shouldContinue = true;
        var frame = timelineFrame;
        if (_isLooping)
        {
            frame = WrapFrame(frame, _endFrame);
        }
        else if (frame >= _endFrame)
        {
            frame = _endFrame;
            shouldContinue = false;
        }

        SeekSelectedAnimationFrame(frame, pauseNonStaticPlayback: false, updateSlider: true, updateDetails: false);
        return shouldContinue;
    }

    private void SeekSelectedAnimationFrame(double frame, bool pauseNonStaticPlayback, bool updateSlider, bool updateDetails)
    {
        if (GetSelectedAnimation() is null)
        {
            return;
        }

        _currentFrame = Math.Clamp(frame, 0, _endFrame);
        ActivateSelectedAnimationPreview();
        UpdateSelectedAnimationSlot();
        if (pauseNonStaticPlayback && !IsStaticSelectorSlot(_selectedAnimationIndex))
        {
            PausePlayback(updateControls: false);
        }

        UpdateAnimationControls(updateSlider: updateSlider);
        RebuildRender(fitSelectionPreview: false, updateDetails: updateDetails);
    }

    private void StopPlayback(bool rebuild = true)
    {
        PausePlayback(updateControls: false);
        _currentFrame = 0;
        ActivateSelectedAnimationPreview();
        UpdateSelectedAnimationSlot();
        UpdateAnimationControls();
        if (rebuild)
        {
            RebuildRender(fitSelectionPreview: false);
        }
    }

    private void RefreshAnimationList()
    {
        PausePlayback(updateControls: false);
        _animationItems.Clear();
        _animationSlots.Clear();
        _selectedAnimationIndex = null;
        _previewAnimationIndex = null;
        _currentFrame = 0;
        _endFrame = 0;
        _isLooping = false;

        if (_scene is not null)
        {
            for (var i = 0; i < _scene.Surfboard.Animations.Count; i++)
            {
                var animation = _scene.Surfboard.Animations[i];
                _animationItems.Add(new AnimationListItem(i, FormatAnimationDisplayName(animation)));
                _animationSlots.Add(new AnimationPlaybackSlot
                {
                    IsLooping = ReadDefaultLoop(animation),
                });
            }
        }

        if (!_controlsReady || AnimationComboBox is null)
        {
            return;
        }

        _isUpdatingAnimationControls = true;
        try
        {
            AnimationComboBox.ItemsSource = null;
            AnimationComboBox.ItemsSource = _animationItems;
            AnimationComboBox.DisplayMemberPath = nameof(AnimationListItem.DisplayName);
            AnimationComboBox.SelectedIndex = _animationItems.Count > 0 ? 0 : -1;
        }
        finally
        {
            _isUpdatingAnimationControls = false;
        }

        if (_animationItems.Count > 0)
        {
            SelectAnimation(_animationItems[0].Index, rebuild: false, activatePreview: false);
        }
        else
        {
            UpdateAnimationControls();
        }
    }

    private void SelectAnimation(int? animationIndex, bool rebuild, bool activatePreview)
    {
        _selectedAnimationIndex = animationIndex;
        _currentFrame = 0;
        _endFrame = 0;
        _isLooping = false;
        if (activatePreview)
        {
            _previewAnimationIndex = animationIndex;
        }

        if (GetSelectedAnimation() is AnimationInfo animation)
        {
            _endFrame = ComputeAnimationEndFrame(animation);
            if (animationIndex is int index && TryGetAnimationSlot(index, out var slot))
            {
                slot.Frame = Math.Clamp(slot.Frame, 0, _endFrame);
                _currentFrame = slot.Frame;
                _isLooping = slot.IsLooping;
            }
        }

        UpdateAnimationControls();
        if (rebuild)
        {
            RebuildRender(fitSelectionPreview: false);
        }
    }

    private AnimationInfo? GetSelectedAnimation()
    {
        if (_scene is null || _selectedAnimationIndex is not int index)
        {
            return null;
        }

        return index >= 0 && index < _scene.Surfboard.Animations.Count
            ? _scene.Surfboard.Animations[index]
            : null;
    }

    private void UpdateAnimationControls(bool updateSlider = true)
    {
        if (!_controlsReady || AnimationCountTextBlock is null)
        {
            return;
        }

        var animationCount = _scene?.Surfboard.Animations.Count ?? 0;
        var lockedAnimationCount = _animationSlots.Count(static slot => slot.IsLocked);
        var hasActiveAnimation = HasActiveAnimationSlot();
        var hasAnimation = GetSelectedAnimation() is not null;
        var selectedAnimationActive = IsSelectedAnimationSlotActive();
        var selectedAnimationLocked = IsSelectedAnimationSlotLocked();
        _isUpdatingAnimationControls = true;
        try
        {
            AnimationCountTextBlock.Text = lockedAnimationCount > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0:N0} animations, {1:N0} locked", animationCount, lockedAnimationCount)
                : string.Format(CultureInfo.InvariantCulture, "{0:N0} animations", animationCount);
            AnimationComboBox.IsEnabled = animationCount > 0;
            AnimationFrameSlider.IsEnabled = hasAnimation;
            AnimationFrameSlider.Maximum = Math.Max(0, _endFrame);
            AnimationFrameSlider.TickFrequency = _endFrame > 0 ? Math.Max(1, Math.Round(_endFrame / 20.0)) : 1;
            if (updateSlider)
            {
                AnimationFrameSlider.Value = Math.Clamp(_currentFrame, AnimationFrameSlider.Minimum, AnimationFrameSlider.Maximum);
            }

            AnimationFrameTextBlock.Text = hasAnimation
                ? $"Frame {FormatFrame(_currentFrame)} / {FormatFrame(_endFrame)}"
                : "Frame 0 / 0";
            PlayAnimationButton.IsEnabled = hasAnimation && !_isPlaying;
            PauseAnimationButton.IsEnabled = hasAnimation && _isPlaying;
            StopAnimationButton.IsEnabled = hasAnimation && (_isPlaying || selectedAnimationActive && Math.Abs(_currentFrame) > 0.0001);
            LockAnimationSlotCheckBox.IsEnabled = hasAnimation;
            LockAnimationSlotCheckBox.IsChecked = hasAnimation && selectedAnimationLocked;
            LoopAnimationCheckBox.IsEnabled = hasAnimation;
            LoopAnimationCheckBox.IsChecked = hasAnimation && _isLooping;
            ResetAnimationStatesButton.IsEnabled = hasActiveAnimation;
        }
        finally
        {
            _isUpdatingAnimationControls = false;
        }
    }

    private static string FormatAnimationDisplayName(AnimationInfo animation)
    {
        return string.IsNullOrWhiteSpace(animation.Name)
            ? $"ANIM@0x{animation.Offset:X}"
            : animation.Name!;
    }

    private static double ComputeAnimationEndFrame(AnimationInfo animation)
    {
        if (TryGetAnimationInt(animation, "0x0056", out var declaredEndFrame) && declaredEndFrame >= 0)
        {
            return declaredEndFrame;
        }

        var maxTrackFrame = animation.Motions
            .SelectMany(static motion => motion.Tracks)
            .Select(static track => track.LastFrame)
            .Where(static frame => frame is >= 0)
            .DefaultIfEmpty()
            .Max();
        if (maxTrackFrame is int trackFrame)
        {
            return trackFrame;
        }

        var maxKeyFrame = animation.Motions
            .SelectMany(static motion => motion.Tracks)
            .SelectMany(static track => track.Keyframes)
            .Select(static key => key.KeyFrame)
            .Where(static frame => frame is >= 0)
            .DefaultIfEmpty()
            .Max();
        return maxKeyFrame ?? 0;
    }

    private static bool ReadDefaultLoop(AnimationInfo animation)
    {
        return TryGetAnimationInt(animation, "0x005F", out var value) && value == 1;
    }

    private static bool TryGetAnimationInt(AnimationInfo animation, string idHex, out int value)
    {
        var raw = animation.NumericFields
            .FirstOrDefault(field => string.Equals(field.IdHex, idHex, StringComparison.Ordinal))?
            .Int64Values?
            .FirstOrDefault();
        if (raw is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)raw.Value;
            return true;
        }

        value = 0;
        return false;
    }

    private static string FormatFrame(double frame)
    {
        return frame.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static double WrapFrame(double frame, double endFrame)
    {
        if (endFrame <= 0)
        {
            return 0;
        }

        var span = endFrame + 1.0;
        frame %= span;
        if (frame < 0)
        {
            frame += span;
        }

        return frame > endFrame ? 0 : frame;
    }

    private static bool IsStaticSelectorSlot(int? index)
    {
        return index is >= 1 and <= 3;
    }

    private void ActivateSelectedAnimationPreview()
    {
        if (_selectedAnimationIndex is int index && TryGetAnimationSlot(index, out _))
        {
            _previewAnimationIndex = index;
        }
    }

    private void SetSelectedAnimationSlotLocked(bool locked)
    {
        if (_selectedAnimationIndex is not int index || !TryGetAnimationSlot(index, out var slot))
        {
            return;
        }

        slot.IsLocked = locked;
    }

    private void UpdateSelectedAnimationSlot()
    {
        if (_selectedAnimationIndex is not int index || !TryGetAnimationSlot(index, out var slot))
        {
            return;
        }

        slot.Frame = Math.Clamp(_currentFrame, 0, _endFrame);
        slot.IsLooping = _isLooping;
    }

    private void SyncSelectedAnimationFieldsFromSlot()
    {
        if (_selectedAnimationIndex is int index && GetSelectedAnimation() is AnimationInfo animation && TryGetAnimationSlot(index, out var slot))
        {
            _endFrame = ComputeAnimationEndFrame(animation);
            slot.Frame = Math.Clamp(slot.Frame, 0, _endFrame);
            _currentFrame = slot.Frame;
            _isLooping = slot.IsLooping;
            return;
        }

        _currentFrame = 0;
        _endFrame = 0;
        _isLooping = false;
    }

    private bool IsSelectedAnimationSlotLocked()
    {
        return _selectedAnimationIndex is int index
            && TryGetAnimationSlot(index, out var slot)
            && slot.IsLocked;
    }

    private bool IsSelectedAnimationSlotActive()
    {
        return _selectedAnimationIndex is int index
            && TryGetAnimationSlot(index, out var slot)
            && IsAnimationSlotActive(slot, index);
    }

    private bool HasActiveAnimationSlot()
    {
        return _animationSlots
            .Select((slot, index) => new { Slot = slot, Index = index })
            .Any(item => IsAnimationSlotActive(item.Slot, item.Index));
    }

    private bool IsAnimationSlotActive(AnimationPlaybackSlot slot, int index)
    {
        return slot.IsLocked || _previewAnimationIndex == index;
    }

    private bool TryGetAnimationSlot(int index, out AnimationPlaybackSlot slot)
    {
        if (index >= 0 && index < _animationSlots.Count)
        {
            slot = _animationSlots[index];
            return true;
        }

        slot = null!;
        return false;
    }

    private void ResetAnimationSlots()
    {
        _previewAnimationIndex = null;
        if (_scene is null)
        {
            _animationSlots.Clear();
            return;
        }

        for (var i = 0; i < _animationSlots.Count && i < _scene.Surfboard.Animations.Count; i++)
        {
            var slot = _animationSlots[i];
            slot.IsLocked = false;
            slot.Frame = 0;
            slot.IsLooping = ReadDefaultLoop(_scene.Surfboard.Animations[i]);
        }
    }

    private IReadOnlyList<RenderSceneAnimationState> BuildActiveAnimationStates()
    {
        if (_scene is null || _animationSlots.Count == 0)
        {
            return [];
        }

        return _animationSlots
            .Select((slot, index) => new { Slot = slot, Index = index })
            .Where(item => IsAnimationSlotActive(item.Slot, item.Index) && item.Index >= 0 && item.Index < _scene.Surfboard.Animations.Count)
            .Select(item => new RenderSceneAnimationState(_scene.Surfboard.Animations[item.Index], item.Slot.Frame))
            .ToArray();
    }

    private void RebuildRender(bool fitSelectionPreview = true, bool updateDetails = true)
    {
        if (!_controlsReady
            || RenderSurface is null
            || SelectionRenderSurface is null
            || EmptyOverlay is null
            || SelectionEmptyOverlay is null)
        {
            return;
        }

        if (_scene is null)
        {
            RenderSurface.Scene = null;
            SelectionRenderSurface.Scene = null;
            SetSelectionPreviewEmpty("未选择节点。", visible: true);
            EmptyOverlay.Visibility = Visibility.Visible;
            return;
        }

        var options = new RenderSceneOptions(
            ShowHiddenCheckBox.IsChecked == true,
            ShowMarkersCheckBox.IsChecked == true,
            _hiddenNodeIndexes,
            _shownNodeIndexes,
            BuildActiveAnimationStates());
        _renderBuildCache ??= new SceneRenderBuildCache(_scene);
        _renderScene = SceneRenderBuilder.Build(_renderBuildCache, _resources, options);
        UpdateRenderSurfaceScene(fitSelectionPreview: fitSelectionPreview && _selectedNodeIndexes is { Count: > 0 });
        if (updateDetails)
        {
            UpdateSelectedNodeInfo();
        }

        EmptyOverlay.Visibility = Visibility.Collapsed;
        if (updateDetails)
        {
            UpdateStatusFromRender();
        }
    }

    private void UpdateSceneSummary()
    {
        if (_scene is null)
        {
            ClearScene();
            return;
        }

        var scene = _scene;
        SceneTitleTextBlock.Text = Path.GetFileName(scene.SourcePath);
        SceneSummaryTextBlock.Text = string.Format(
            CultureInfo.InvariantCulture,
            "{0:N0} nodes, {1:N0} image casts, {2:N0} atlases, {3:N0} animations",
            scene.Surfboard.Nodes.Count,
            scene.Surfboard.Resources.ImageCasts.Count,
            scene.Surfboard.Resources.Atlases.Count,
            scene.Surfboard.Animations.Count);

        SvoSummaryTextBlock.Text = _resources is null
            ? "未绑定 SVO；以占位色绘制 CIMG。"
            : string.Format(
                CultureInfo.InvariantCulture,
                "SVO: {0} ({1:N0}/{2:N0} atlases decoded)",
                Path.GetFileName(_resources.Path),
                _resources.AtlasImages.Count,
                scene.Surfboard.Resources.Atlases.Count);
    }

    private void UpdateStatusFromRender()
    {
        if (_scene is null || _renderScene is null)
        {
            SetStatus("就绪。");
            return;
        }

        var images = _renderScene.Items.Count(static item => item.Kind == "image");
        var bitmaps = _renderScene.Items.Count(static item => item.Bitmap is not null);
        var missing = images - bitmaps;
        var warnings = BuildWarningSummary();
        var status = string.Format(
            CultureInfo.InvariantCulture,
            "绘制 {0:N0} items，bitmap {1:N0}/{2:N0}，占位 {3:N0}。{4}",
            _renderScene.Items.Count,
            bitmaps,
            images,
            missing,
            warnings);
        SetStatus(status.TrimEnd());
    }

    private void UpdateSelectedNodeInfo()
    {
        if (!_controlsReady || SelectedNodeInfoTextBlock is null)
        {
            return;
        }

        if (_scene is null)
        {
            SelectedNodeInfoTextBlock.Text = "未加载 scene。";
            return;
        }

        if (_selectedNodeIndex is not int nodeIndex
            || nodeIndex < 0
            || nodeIndex >= _scene.Surfboard.Nodes.Count)
        {
            SelectedNodeInfoTextBlock.Text = "未选择节点。";
            return;
        }

        var node = _scene.Surfboard.Nodes[nodeIndex];
        var selectedIndexes = _selectedNodeIndexes ?? [nodeIndex];
        var imageCastCount = _scene.Surfboard.Resources.ImageCasts.Count(imageCast => selectedIndexes.Contains(imageCast.CastIndex));
        var itemCount = _renderScene?.Items.Count(item => selectedIndexes.Contains(item.NodeIndex)) ?? 0;
        var builder = new StringBuilder();
        builder.AppendLine($"Index:      {node.Index}");
        builder.AppendLine($"Name:       {node.Name ?? "(unnamed)"}");
        builder.AppendLine($"Group:      {node.Group}");
        builder.AppendLine($"Display:    {FormatDisplayState(node)}");
        builder.AppendLine($"Flags:      {(node.Flags is null ? "-" : $"0x{node.Flags.Value:X}")}");
        builder.AppendLine($"Subtree:    {selectedIndexes.Count:N0} nodes");
        builder.AppendLine($"Images:     {imageCastCount:N0} image casts, {itemCount:N0} render items");
        builder.AppendLine($"Transform:  {FormatTransform(node.Transform2D)}");
        if (!string.IsNullOrWhiteSpace(node.Comment))
        {
            builder.AppendLine($"Comment:    {node.Comment}");
        }

        builder.AppendLine($"Path:       {node.Path}");
        SelectedNodeInfoTextBlock.Text = builder.ToString();
    }

    private string FormatDisplayState(NodeInfo node)
    {
        if (_hiddenNodeIndexes.Contains(node.Index))
        {
            return "hide* (viewer override)";
        }

        if (_shownNodeIndexes.Contains(node.Index))
        {
            return "show* (viewer override)";
        }

        return node.Transform2D?.Display switch
        {
            true => "show",
            false => "hide",
            _ => "?",
        };
    }

    private static string FormatTransform(Transform2DInfo? transform)
    {
        if (transform is null)
        {
            return "-";
        }

        var tx = transform.Translation?.X.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var ty = transform.Translation?.Y.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var sx = transform.Scale?.X.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var sy = transform.Scale?.Y.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var rotation = (transform.RotationZDegreesCandidate ?? transform.RotationZ)?.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        return $"T({tx},{ty}) R({rotation}) S({sx},{sy})";
    }

    private string BuildWarningSummary()
    {
        var warningCount = (_scene?.Summary.Warnings.Count ?? 0) + (_resources?.Warnings.Count ?? 0) + (_renderScene?.Warnings.Count ?? 0);
        if (warningCount == 0)
        {
            return string.Empty;
        }

        var firstWarning = _scene?.Summary.Warnings.FirstOrDefault()
            ?? _resources?.Warnings.FirstOrDefault()
            ?? _renderScene?.Warnings.FirstOrDefault();
        return $"警告 {warningCount}: {firstWarning}";
    }

    private void ClearScene()
    {
        _scene = null;
        _renderBuildCache = null;
        _resources = null;
        _renderScene = null;
        _scenePath = null;
        _selectedNodeIndex = null;
        _selectedNodeIndexes = null;
        _hiddenNodeIndexes.Clear();
        _shownNodeIndexes.Clear();
        RefreshAnimationList();
        NodeTree.ItemsSource = null;
        RenderSurface.Scene = null;
        RenderSurface.PrimaryHighlightedNodeIndex = null;
        RenderSurface.HighlightedNodeIndexes = new HashSet<int>();
        SelectionRenderSurface.Scene = null;
        SelectionRenderSurface.PrimaryHighlightedNodeIndex = null;
        SelectionRenderSurface.HighlightedNodeIndexes = new HashSet<int>();
        SceneTitleTextBlock.Text = "未加载 scene";
        SceneSummaryTextBlock.Text = string.Empty;
        SvoSummaryTextBlock.Text = string.Empty;
        UpdateSelectedNodeInfo();
        SetSelectionPreviewEmpty("未选择节点。", visible: true);
        EmptyOverlay.Visibility = Visibility.Visible;
    }

    private void FitSceneToViewport()
    {
        if (RenderSurface.Scene is null || SceneScrollViewer.ViewportWidth <= 0 || SceneScrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        RenderSurface.CaptureCurrentContentBounds();
        var size = RenderSurface.SurfaceSize;
        var zoom = Math.Min(SceneScrollViewer.ViewportWidth / size.Width, SceneScrollViewer.ViewportHeight / size.Height);
        SetZoom(Math.Clamp(zoom, 0.1, 4));
        SceneScrollViewer.ScrollToHorizontalOffset(0);
        SceneScrollViewer.ScrollToVerticalOffset(0);
    }

    private void SelectNode(int nodeIndex)
    {
        _selectedNodeIndex = nodeIndex;
        _selectedNodeIndexes = [nodeIndex];
        UpdateRenderSurfaceScene(fitSelectionPreview: true);
        UpdateSelectedNodeInfo();
        if (!_controlsReady || NodeTree.Items.Count == 0)
        {
            return;
        }

        NodeTree.UpdateLayout();
        if (TrySelectTreeItem(NodeTree, nodeIndex, out var item))
        {
            item.IsSelected = true;
            item.Focus();
            item.BringIntoView();
            if (item.DataContext is NodeTreeItem treeItem)
            {
                HighlightNodeSubtree(treeItem);
            }
        }
    }

    private void UpdateRenderSurfaceScene(bool fitSelectionPreview)
    {
        if (!_controlsReady || RenderSurface is null || SelectionRenderSurface is null)
        {
            return;
        }

        UpdateRenderSurfaceMinimumWidth();

        if (_renderScene is null)
        {
            RenderSurface.Scene = null;
            RenderSurface.PrimaryHighlightedNodeIndex = null;
            RenderSurface.HighlightedNodeIndexes = new HashSet<int>();
            SelectionRenderSurface.Scene = null;
            SelectionRenderSurface.PrimaryHighlightedNodeIndex = null;
            SelectionRenderSurface.HighlightedNodeIndexes = new HashSet<int>();
            SetSelectionPreviewEmpty("未选择节点。", visible: true);
            return;
        }

        RenderSurface.Scene = _renderScene;
        RenderSurface.PrimaryHighlightedNodeIndex = _selectedNodeIndex;
        RenderSurface.HighlightedNodeIndexes = _selectedNodeIndexes ?? new HashSet<int>();

        if (_selectedNodeIndexes is not { Count: > 0 } indexes)
        {
            SelectionRenderSurface.Scene = null;
            SelectionRenderSurface.PrimaryHighlightedNodeIndex = null;
            SelectionRenderSurface.HighlightedNodeIndexes = new HashSet<int>();
            SetSelectionPreviewEmpty("未选择节点。", visible: true);
            return;
        }

        var selectionScene = FilterRenderScene(_renderScene, indexes);
        SelectionRenderSurface.Scene = selectionScene;
        SelectionRenderSurface.PrimaryHighlightedNodeIndex = _selectedNodeIndex;
        SelectionRenderSurface.HighlightedNodeIndexes = indexes;
        SetSelectionPreviewEmpty(
            selectionScene.Items.Count == 0 ? "选中子树没有可绘制项。" : null,
            visible: selectionScene.Items.Count == 0);

        if (fitSelectionPreview)
        {
            FitSelectionPreviewToViewport();
        }
    }

    private void UpdateRenderSurfaceMinimumWidth()
    {
        if (!_controlsReady || RenderSurface is null || SceneScrollViewer is null)
        {
            return;
        }

        var width = SceneScrollViewer.ViewportWidth;
        if (!double.IsFinite(width) || width <= 0)
        {
            width = SceneScrollViewer.ActualWidth;
        }

        RenderSurface.MinWidth = Math.Max(0, width);
    }

    private void FitSelectionPreviewToViewport()
    {
        if (SelectionRenderSurface.Scene is null
            || SelectionScrollViewer.ViewportWidth <= 0
            || SelectionScrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        SelectionScrollViewer.UpdateLayout();
        SelectionRenderSurface.CaptureCurrentContentBounds();
        var size = SelectionRenderSurface.SurfaceSize;
        var zoom = Math.Min(
            SelectionScrollViewer.ViewportWidth / size.Width,
            SelectionScrollViewer.ViewportHeight / size.Height);
        SelectionRenderSurface.Zoom = Math.Clamp(zoom, 0.1, 4);
        SelectionScrollViewer.ScrollToHorizontalOffset(0);
        SelectionScrollViewer.ScrollToVerticalOffset(0);
    }

    private void SelectionScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        FitSelectionPreviewToViewport();
    }

    private void SetSelectionPreviewEmpty(string? text, bool visible)
    {
        if (SelectionEmptyTextBlock is not null && text is not null)
        {
            SelectionEmptyTextBlock.Text = text;
        }

        if (SelectionEmptyOverlay is not null)
        {
            SelectionEmptyOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static RenderScene FilterRenderScene(RenderScene source, IReadOnlySet<int> nodeIndexes)
    {
        var items = source.Items
            .Where(item => nodeIndexes.Contains(item.NodeIndex))
            .ToArray();
        var bounds = Rect.Empty;
        foreach (var item in items)
        {
            bounds.Union(item.WorldBounds);
        }

        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            bounds = new Rect(-160, -120, 320, 240);
        }

        return new RenderScene
        {
            Items = items,
            ContentBounds = bounds,
            Warnings = source.Warnings,
        };
    }

    private void SetSelectedSubtreeVisibility(bool? visible)
    {
        if (NodeTree.SelectedItem is not NodeTreeItem item)
        {
            SetStatus("请先选择一个节点。");
            return;
        }

        var indexes = item.EnumerateSelfAndDescendants()
            .Select(static node => node.Index)
            .ToArray();
        foreach (var index in indexes)
        {
            if (visible == true)
            {
                _hiddenNodeIndexes.Remove(index);
                _shownNodeIndexes.Add(index);
            }
            else if (visible == false)
            {
                _shownNodeIndexes.Remove(index);
                _hiddenNodeIndexes.Add(index);
            }
            else
            {
                _hiddenNodeIndexes.Remove(index);
                _shownNodeIndexes.Remove(index);
            }
        }

        var selectedIndex = item.Index;
        var selectedIndexes = indexes.ToHashSet();
        _selectedNodeIndex = selectedIndex;
        _selectedNodeIndexes = selectedIndexes;
        RefreshNodeTree();
        _selectedNodeIndex = selectedIndex;
        _selectedNodeIndexes = selectedIndexes;
        RebuildRender();
        UpdateRenderSurfaceScene(fitSelectionPreview: true);
        UpdateSelectedNodeInfo();
        NodeTree.UpdateLayout();
        if (TrySelectTreeItem(NodeTree, selectedIndex, out var selectedTreeItem))
        {
            selectedTreeItem.IsSelected = true;
            selectedTreeItem.Focus();
            selectedTreeItem.BringIntoView();
        }

        var action = visible switch
        {
            true => "显示",
            false => "隐藏",
            _ => "恢复",
        };
        SetStatus($"{action} {indexes.Length:N0} 个节点。");
    }

    private void RefreshNodeTree()
    {
        if (_scene is null || !_controlsReady || NodeTree is null)
        {
            return;
        }

        NodeTree.ItemsSource = SceneRenderBuilder.BuildNodeTree(_scene, _hiddenNodeIndexes, _shownNodeIndexes);
    }

    private void HighlightNodeSubtree(NodeTreeItem item)
    {
        var indexes = item.EnumerateSelfAndDescendants()
            .Select(static node => node.Index)
            .ToHashSet();
        _selectedNodeIndex = item.Index;
        _selectedNodeIndexes = indexes;
        UpdateRenderSurfaceScene(fitSelectionPreview: true);
        UpdateSelectedNodeInfo();
    }

    private void SetZoom(double value, bool updateSlider = true)
    {
        var minimum = ZoomSlider is null ? 0.1 : ZoomSlider.Minimum;
        var maximum = ZoomSlider is null ? 4 : ZoomSlider.Maximum;
        var zoom = Math.Clamp(value, minimum, maximum);
        if (RenderSurface is not null)
        {
            RenderSurface.Zoom = zoom;
        }

        if (updateSlider && ZoomSlider is not null && Math.Abs(ZoomSlider.Value - zoom) > 0.0001)
        {
            ZoomSlider.Value = zoom;
        }

        if (ZoomTextBlock is not null)
        {
            ZoomTextBlock.Text = zoom.ToString("P0", CultureInfo.InvariantCulture);
        }
    }

    private IDisposable BeginBusy(string status)
    {
        SetStatus(status);
        Mouse.OverrideCursor = Cursors.Wait;
        return new BusyScope(() => Mouse.OverrideCursor = null);
    }

    private void SetStatus(string status)
    {
        StatusTextBlock.Text = status;
    }

    private static string? FindSiblingSvo(string scenePath)
    {
        var exact = Path.ChangeExtension(scenePath, ".svo");
        if (File.Exists(exact))
        {
            return exact;
        }

        var directory = Path.GetDirectoryName(scenePath);
        if (directory is null || !Directory.Exists(directory))
        {
            return null;
        }

        var candidates = Directory.GetFiles(directory, "*.svo");
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static bool IsSceneFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".sbscene", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSvoFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".svo", StringComparison.OrdinalIgnoreCase);
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T target)
            {
                return target;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static bool TrySelectTreeItem(ItemsControl parent, int nodeIndex, out TreeViewItem item)
    {
        parent.ApplyTemplate();
        parent.UpdateLayout();

        foreach (var sourceItem in parent.Items)
        {
            var container = parent.ItemContainerGenerator.ContainerFromItem(sourceItem) as TreeViewItem;
            if (container is null)
            {
                continue;
            }

            if (sourceItem is NodeTreeItem node && node.Index == nodeIndex)
            {
                item = container;
                return true;
            }

            container.IsExpanded = true;
            container.ApplyTemplate();
            container.UpdateLayout();
            if (TrySelectTreeItem(container, nodeIndex, out item))
            {
                return true;
            }
        }

        item = null!;
        return false;
    }

    private sealed class BusyScope : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public BusyScope(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _dispose();
            _disposed = true;
        }
    }

    private sealed record AnimationListItem(int Index, string DisplayName);

    private sealed class AnimationPlaybackSlot
    {
        public bool IsLocked { get; set; }

        public double Frame { get; set; }

        public bool IsLooping { get; set; }
    }
}
