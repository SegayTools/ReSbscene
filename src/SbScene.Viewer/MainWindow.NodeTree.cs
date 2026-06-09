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
}
