namespace WatcherCore;

public record Args(string Path, bool SnakeCase, bool GenerateExtension, bool GenerateString, bool IgnoreRoot);