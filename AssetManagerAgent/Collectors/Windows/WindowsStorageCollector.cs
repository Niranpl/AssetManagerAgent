using System.Diagnostics;
using System.Management;
using System.Text.Json;
using System.Collections.Generic;
using AssetManagerAgent.Core.Models;

namespace AssetManagerAgent.Collectors.Windows;

public class WindowsStorageCollector
{
    public List<StorageDevice> Collect()
    {
        var disks = new List<StorageDevice>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_DiskDrive");

            foreach (ManagementObject disk in searcher.Get())
            {
                var device = GetString(disk, "DeviceID");
                var serial = GetString(disk, "SerialNumber");
                var model = GetString(disk, "Model");
                var manufacturer = GetString(disk, "Manufacturer");
                var firmware = GetString(disk, "FirmwareRevision");
                var mediaType = GetString(disk, "MediaType");
                var interfaceType = GetString(disk, "InterfaceType");
                var size = GetLong(disk, "Size");

                disks.Add(new StorageDevice
                {
                    Name = model,
                    Device = device,
                    Manufacturer = manufacturer,
                    Model = model,
                    SerialNumber = serial,

                    CapacityGB = Math.Round(
                        size / 1024d / 1024d / 1024d,
                        2),

                    MediaType = DetermineMediaType(
                        disk,
                        mediaType,
                        model),

                    Protocol = interfaceType,
                    FirmwareRevision = firmware,

                    PhysicalIdentifier = BuildPhysicalIdentifier(
                        device,
                        serial,
                        model),

                    HardwareIdentifier = BuildPhysicalIdentifier(
                        device,
                        serial,
                        model),

                   Internal =
    !interfaceType.Equals(
        "USB",
        StringComparison.OrdinalIgnoreCase),

                    SolidState = IsSolidState(
                        disk,
                        mediaType,
                        model),

                    Virtual = IsVirtualDisk(
                        model,
                        device),

                    SmartStatus = GetString(
                        disk,
                        "Status"),

                    Volumes = GetVolumes(device)
                });
            }
        }
        catch
        {
        }

        EnrichPhysicalDiskDetails(disks);
	SetWindowsStorageCapabilities(disks);	

        return disks;
    }


private static List<StorageVolume> GetVolumes(
    string physicalDisk)
{
    var volumes = new List<StorageVolume>();

    try
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            physicalDisk ?? string.Empty,
            @"PHYSICALDRIVE(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
            return volumes;

        var diskNumber = match.Groups[1].Value;

        var command =
            "$logical = Get-CimInstance Win32_LogicalDisk " +
            "| Where-Object { $_.DriveType -eq 3 }; " +

            "Get-Partition -DiskNumber " + diskNumber +
            " | Where-Object { $null -ne $_.DriveLetter } " +
            "| ForEach-Object { " +

            "$letter = $_.DriveLetter; " +

            "$v = $logical | Where-Object { " +
            "$_.DeviceID -eq ($letter + ':') }; " +

            "if ($null -ne $v) { " +

            "[PSCustomObject]@{" +
            "DriveLetter=$letter;" +
            "FileSystem=$v.FileSystem;" +
            "FileSystemLabel=$v.VolumeName;" +
            "Size=$v.Size;" +
            "SizeRemaining=$v.FreeSpace" +
            "} " +

            "} " +

            "} | ConvertTo-Csv -NoTypeInformation";

        var output = RunPowerShell(command);

        if (string.IsNullOrWhiteSpace(output))
            return volumes;

        var lines = output
            .Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
            return volumes;

        foreach (var line in lines.Skip(1))
        {
            var fields = ParseCsvLine(line);

            if (fields.Count < 5)
                continue;

long.TryParse(fields[3], out var size);
long.TryParse(fields[4], out var free);

            var used = Math.Max(0, size - free);

            volumes.Add(new StorageVolume
            {
                Name = fields[2],
                MountPoint = fields[0] + ":",
                FileSystem = fields[1],
                BsdName = string.Empty,
                VolumeUuid = string.Empty,

                CapacityGB =
                    size / 1024d / 1024d / 1024d,

                UsedGB =
                    used / 1024d / 1024d / 1024d,

                FreeGB =
                    free / 1024d / 1024d / 1024d,

                Writable = true
            });
        }
    }
    catch
    {
    }

    return volumes;
}

private static void SetWindowsStorageCapabilities(
    List<StorageDevice> disks)
{
    try
    {
        var output = RunPowerShell(
            "fsutil behavior query DisableDeleteNotify");

        if (string.IsNullOrWhiteSpace(output))
            return;

        var trimSupported =
            output.Contains(
                "NTFS DisableDeleteNotify = 0",
                StringComparison.OrdinalIgnoreCase) ||
            output.Contains(
                "ReFS DisableDeleteNotify = 0",
                StringComparison.OrdinalIgnoreCase);

        foreach (var disk in disks)
        {
            disk.TrimSupported = trimSupported;

            if (disk.Volumes != null &&
                disk.Volumes.Count > 0)
            {
                disk.UsedGB =
                    disk.Volumes.Sum(v => v.UsedGB);

                disk.FreeGB =
                    disk.Volumes.Sum(v => v.FreeGB);
            }
        }
    }
    catch
    {
    }
}

    private static void EnrichPhysicalDiskDetails(
        List<StorageDevice> disks)
    {
        var json = RunPowerShell(
            "Get-PhysicalDisk | " +
            "Select-Object FriendlyName,SerialNumber,MediaType,BusType,Size,HealthStatus,FirmwareVersion " +
            "| ConvertTo-Json -Compress");

        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var document = JsonDocument.Parse(json);

IEnumerable<JsonElement> items;

if (document.RootElement.ValueKind == JsonValueKind.Array)
{
    items = document.RootElement.EnumerateArray().ToList();
}
else
{
    items = new List<JsonElement>
    {
        document.RootElement
    };
}
            foreach (var item in items)
            {
                var serial = GetJsonString(
                    item,
                    "SerialNumber");

                if (string.IsNullOrWhiteSpace(serial))
                    continue;

                var disk = disks.FirstOrDefault(x =>
                    string.Equals(
                        x.SerialNumber?.Trim(),
                        serial.Trim(),
                        StringComparison.OrdinalIgnoreCase));

                if (disk == null)
                    continue;

                var mediaType = GetJsonString(
                    item,
                    "MediaType");

                var busType = GetJsonString(
                    item,
                    "BusType");

                var healthStatus = GetJsonString(
                    item,
                    "HealthStatus");

                var firmware = GetJsonString(
                    item,
                    "FirmwareVersion");

                if (!string.IsNullOrWhiteSpace(mediaType))
                    disk.MediaType = mediaType;
                        if (string.Equals(
                        mediaType,
                        "SSD",
                        StringComparison.OrdinalIgnoreCase))
                        {
                            disk.SolidState = true;
                        }
                if (!string.IsNullOrWhiteSpace(busType))
                    disk.Protocol = busType;
if (string.Equals(
        busType,
        "USB",
        StringComparison.OrdinalIgnoreCase))
{
    disk.Internal = false;
}
else if (!string.IsNullOrWhiteSpace(busType))
{
    disk.Internal = true;
}

                if (!string.IsNullOrWhiteSpace(healthStatus))
                    disk.SmartStatus = healthStatus;

                if (!string.IsNullOrWhiteSpace(firmware))
                    disk.FirmwareRevision = firmware;
            }
        }
        catch
        {
        }
    }

    private static string RunPowerShell(
        string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    $"-NoProfile -NonInteractive -Command \"{command}\"",

                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            if (process == null)
                return string.Empty;

            var output =
                process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            return output.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetJsonString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property))
        {
            return string.Empty;
        }

        return property.ValueKind == JsonValueKind.Null
            ? string.Empty
            : property.ToString().Trim();
    }
    private static long GetJsonLong(
    JsonElement element,
    string propertyName)
{
    if (!element.TryGetProperty(
            propertyName,
            out var property))
    {
        return 0;
    }

    try
    {
        if (property.ValueKind ==
            JsonValueKind.Number)
        {
            return property.GetInt64();
        }

        if (long.TryParse(
                property.ToString(),
                out var value))
        {
            return value;
        }
    }
    catch
    {
    }

    return 0;
}

private static List<string> ParseCsvLine(string line)
{
    var result = new List<string>();
    var current = new System.Text.StringBuilder();
    var insideQuotes = false;

    foreach (var character in line)
    {
        if (character == '"')
        {
            insideQuotes = !insideQuotes;
            continue;
        }

        if (character == ',' && !insideQuotes)
        {
            result.Add(current.ToString());
            current.Clear();
            continue;
        }

        current.Append(character);
    }

    result.Add(current.ToString());

    return result;
}

    private static string BuildPhysicalIdentifier(
        string device,
        string serial,
        string model)
    {
        if (!string.IsNullOrWhiteSpace(serial))
            return $"SERIAL|{serial.Trim()}";

        if (!string.IsNullOrWhiteSpace(device))
            return $"DEVICE|{device.Trim()}";

        return $"MODEL|{model.Trim()}";
    }

    private static string DetermineMediaType(
        ManagementObject disk,
        string mediaType,
        string model)
    {
        if (!string.IsNullOrWhiteSpace(mediaType))
            return mediaType;

        if (IsSolidState(
                disk,
                mediaType,
                model))
        {
            return "SSD";
        }

        return "Unknown";
    }

    private static bool IsSolidState(
        ManagementObject disk,
        string mediaType,
        string model)
    {
        if (mediaType.Contains(
                "SSD",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var lowerModel =
            model.ToLowerInvariant();

        return lowerModel.Contains("ssd") ||
               lowerModel.Contains("nvme") ||
               lowerModel.Contains("solid state");
    }

    private static bool IsVirtualDisk(
        string model,
        string device)
    {
        var value =
            $"{model} {device}";

        return value.Contains(
                   "Virtual",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Contains(
                   "VMware",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Contains(
                   "VirtualBox",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Contains(
                   "Hyper-V",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string GetString(
        ManagementBaseObject obj,
        string property)
    {
        try
        {
            return obj[property]?.ToString()?.Trim()
                   ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static long GetLong(
        ManagementBaseObject obj,
        string property)
    {
        try
        {
            return Convert.ToInt64(
                obj[property]);
        }
        catch
        {
            return 0;
        }
    }
}
