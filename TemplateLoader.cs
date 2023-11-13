using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;

namespace nvrlift.AssettoServer.HostExtension;

public class TemplateLoader : IDisposable
{
    private const string RegexPattern = @"\[\$([a-z0-9_-]+)\]";
    private Dictionary<string, string>? _config;
    private readonly string _templatePath;
    private readonly bool _useEnvVar;
    private readonly string _presetPath;
    
    public TemplateLoader(string basePath, bool useEnvVar)
    {
        _useEnvVar = useEnvVar;
        _presetPath = Path.Join(basePath, "presets");
        _templatePath = Path.Join(basePath, "templates");
    }

    public void Load()
    {
        if (Path.Exists(_presetPath))
        {
            Directory.Move(_presetPath, $"{_presetPath}{DateTime.Now:yyyyMMdd-HHmmss}");
        }

        if (!Path.Exists(_templatePath))
        {
            Directory.CreateDirectory(_templatePath);
            Log.Error($"No template folder found.");
            return;
        }
        
        var cfgPath = Path.Join(_templatePath, "template_cfg.json");
        if (!Path.Exists(cfgPath))
        {
            Log.Error($"template_cfg.json not found.");
            return;
        }
        var json = File.ReadAllText(cfgPath);
        _config = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

        Log.Information($"Starting to copy templates/ into presets/");
        CopyFilesRecursively(new DirectoryInfo(_templatePath), new DirectoryInfo(_presetPath));
        Log.Information($"Copying templates finished.");
    }

    private void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target) {
        foreach (DirectoryInfo dir in source.GetDirectories())
            CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
        foreach (FileInfo file in source.GetFiles())
            CopyFile(file.DirectoryName!, target.FullName, file.Name);
    }
    
    private void CopyFile(string sourcePath, string targetPath, string fileName)
    {
        using var input = File.OpenText(Path.Join(sourcePath, fileName));
        using var output = new StreamWriter(Path.Join(targetPath, fileName));
        string? line;
        while (null != (line = input.ReadLine())) {
            var modifiedLine = Regex.Replace(line, RegexPattern,
                m => TryGetValue(m.Groups[1].Value, out string variableValue) ? variableValue : m.Value, RegexOptions.IgnoreCase);
                
            output.WriteLine(modifiedLine);
        }
    }

    private bool TryGetValue(string property, out string result)
    {
        if (_config!.TryGetValue(property, out var cfgVar))
        {
            if (!string.IsNullOrEmpty(cfgVar))
            {
                result = cfgVar;
                return true;
            }
        }
        else if (_useEnvVar)
        {
            var envVar = Environment.GetEnvironmentVariable(property);
            if (!string.IsNullOrEmpty(envVar))
            {
                result = envVar;
                return true;
            }

            Log.Warning($"Environment variable '{property}' not found.");
        }
        else
            Log.Warning($"Config variable '{property}' not found.");

        result = "";
        return false;
    }

    public void Dispose() { }
}
