using System;
using System.Collections.Generic;

namespace WatcherCore;

public class TreeItem(string fileName, string filePath, string? relativePath, bool directory) {
    public List<TreeItem> TreeItems { get; } = [];

    public string FileName { get; } = fileName;
    public string FilePath { get; } = filePath;
    public string? RelativePath { get; } = relativePath;
    public bool Directory { get; } = directory;
    public int ChildCount => TreeItems.Count;

    public TreeItem CreateChild(string name, string path, string relativePath, bool directory = true) {
        TreeItem treeItem = new(name, path, relativePath, directory);
        TreeItems.Add(treeItem);
        return treeItem;
    }

    public void RemoveChild(TreeItem treeItem) {
        TreeItems.Remove(treeItem);
    }

    public void RemoveChild(int index) {
        TreeItems.RemoveAt(index);
    }

    public void CleanChild() {
        TreeItems.Clear();
    }

    public bool HasFile() {
        var hasFile = false;
        Ergodic(this, item => {
            if (item.Directory)
                return false;
            hasFile = true;
            return true;
        });
        return hasFile;
    }

    private static void Ergodic(TreeItem treeItem, Func<TreeItem, bool> action) {
        foreach (TreeItem item in treeItem.TreeItems) {
            if (action(item))
                return;
            Ergodic(item, action);
        }
    }
}