using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SbScene.Core.Output;
using SbScene.Core.Rendering;
using SbScene.Core.Semantics;
using SbScene.Core.Unity;

namespace SbScene.Viewer;

public partial class MainWindow : Window
{
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

    private sealed record AnimationListItem(int Index, string DisplayName);

    private sealed class AnimationPlaybackSlot
    {
        public bool IsLocked { get; set; }

        public double Frame { get; set; }

        public bool IsLooping { get; set; }
    }
}
