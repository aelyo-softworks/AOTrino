using Microsoft.Win32;

namespace AOTrino;

// read-only facts about the machine that the page has no other way of learning: versions down to the kernel,
// the real cultures and keyboard layouts, DPI, the monitors, the graphics adapters, whether this is a VM or a remote session. informative only,
// nothing here *does* anything.
//
// AOTrino does NOT register this for you, and that is the point.
// Any page a window navigates to can call every host object registered on that window (WebView2's AddHostObjectToScript takes no origin),
// so exposing this has to be your decision, per window:
//
//     protected override void RegisterHostObjects() => AddHostObject("system", new SystemInfo(this));
//
// don't register it on a window that browses the web. see docs/SECURITY.md.
//
// the values are a JSON DOM, built once, and yours to edit before you hand it over:
//
//     var info = new SystemInfo(this);
// info.Values.Remove("adapters"), // this app has no business reporting the GPU info.Values["tenant"] = currentTenant, // ...but it does report this.
//     AddHostObject("system", info);
//
// or override Build() to compose it from scratch.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class SystemInfo : DispatchObject
{
    private const string _unknown = "unknown";

    public SystemInfo(WebViewWindow? window = null)
    {
        Window = window;
        Values = Build();
    }

    // the window this describes, when there is one: it's what knows the DPI actually in use.
    [Browsable(false)]
    protected WebViewWindow? Window { get; }

    // everything the page will see, as a mutable JSON DOM.
    // [Browsable(false)] keeps the DOM itself off the bridge, the page gets it through GetInfo().
    [Browsable(false)]
    public JsonObject Values { get; }

    // the whole thing in one call, rather than a bridge round-trip per value:
    //     const info = JSON.parse(await system.getInfo());
    public string GetInfo() => Values.ToJsonString();

    protected virtual JsonObject Build()
    {
        var app = AOTrinoApplication.Current;
        var entry = Assembly.GetEntryAssembly();
        return new JsonObject
        {
            ["versions"] = BuildVersions(app),
            ["application"] = BuildApplication(entry),
            ["system"] = BuildSystem(),
            ["culture"] = BuildCulture(),
            ["input"] = BuildInput(),
            ["display"] = BuildDisplay(),
            ["displays"] = BuildDisplays(),
            ["adapters"] = BuildAdapters(),
        };
    }

    protected virtual JsonObject BuildVersions(AOTrinoApplication? app) => new()
    {
        ["aotrino"] = app?.AOTrinoVersion,
        ["webView2"] = app?.WebView2Version,
        ["dotNet"] = RuntimeInformation.FrameworkDescription,
        ["directN"] = typeof(DirectNFunctions).Assembly.GetInformationalVersion(),
        // the marketing string, then what the kernel actually reports: OSVersion lies about builds, ntoskrnl doesn't.
        ["windows"] = Environment.OSVersion.VersionString,
        ["kernel"] = WindowsVersionUtilities.KernelVersion?.ToString(),
    };

    protected virtual JsonObject BuildApplication(Assembly? entry) => new()
    {
        ["title"] = entry?.GetCustomAttribute<AssemblyTitleAttribute>()?.Title,
        ["company"] = entry?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company,
        ["product"] = entry?.GetCustomAttribute<AssemblyProductAttribute>()?.Product,
        ["copyright"] = entry?.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright,
        ["version"] = entry?.GetInformationalVersion(),
        ["processId"] = Environment.ProcessId,
    };

    protected virtual JsonObject BuildSystem() => new()
    {
        ["processorCount"] = Environment.ProcessorCount,
        ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
        ["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
        ["is64BitProcess"] = Environment.Is64BitProcess,
        // https://learn.microsoft.com/en-us/windows/win32/tablet/determining-whether-a-pc-is-a-tablet-pc
        ["isTabletPc"] = DirectNFunctions.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_TABLETPC) != 0,
        ["isRemoteSession"] = DirectNFunctions.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_REMOTESESSION) != 0,
        ["isRemotelyControlled"] = DirectNFunctions.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_REMOTECONTROL) != 0,
        ["virtualMachine"] = GetVirtualMachine(),
    };

    protected virtual JsonObject BuildCulture() => new()
    {
        ["current"] = CultureInfo.CurrentCulture.Name,
        ["currentDisplayName"] = CultureInfo.CurrentCulture.NativeName,
        ["currentUI"] = CultureInfo.CurrentUICulture.Name,
        // the language Windows itself was installed in, which is not necessarily the two above.
        ["installedUI"] = CultureInfo.InstalledUICulture.Name,
    };

    protected virtual JsonObject BuildInput()
    {
        // gather into a List<JsonNode?> and use JsonArray's params constructor:
        // JsonArray.Add<T>() is generic, and generic JsonValue creation needs runtime codegen, which AOT doesn't have.
        var items = new List<JsonNode?>();
        foreach (var language in KeyboardUtilities.GetUserLanguages())
        {
            var itemObj = new JsonObject
            {
                ["name"] = language.Name,
            };

            if (language.InputMethodTips.Count > 0)
            {
                var tips = new JsonArray([.. language.InputMethodTips]);
                itemObj["inputMethodTips"] = tips;
            }

            if (language.KeyboardLayout != null)
            {
                itemObj["keyboardLayout"] = new JsonObject
                {
                    ["id"] = language.KeyboardLayout.Id,
                    ["englishName"] = language.KeyboardLayout.EnglishName,
                    ["localizedName"] = language.KeyboardLayout.LocalizedName,
                    ["file"] = language.KeyboardLayout.File,
                    ["tip"] = language.KeyboardLayout.Tip,
                };
            }

            items.Add(itemObj);
        }

        var obj = new JsonObject
        {
            ["userLanguages"] = new JsonArray([.. items])
        };

        var tag = Windows.Globalization.Language.CurrentInputMethodLanguageTag;
        if (!string.IsNullOrEmpty(tag))
        {
            obj["currentInputLanguage"] = tag;
        }
        return obj;
    }

    protected virtual JsonObject BuildDisplay()
    {
        var dpi = Window != null ? DpiUtilities.GetDpiForWindow(Window.Handle).width : DpiUtilities.GetDpiForDesktop().width;
        return new JsonObject
        {
            // devicePixelRatio already tells the page its scale, this is the number that scale came from.
            ["dpi"] = dpi,
            ["scale"] = Math.Round(dpi / (double)DirectNConstants.USER_DEFAULT_SCREEN_DPI, 2),
            // the accessibility text size, which nothing in CSS reports.
            ["textScaleFactor"] = DpiUtilities.TextScaleFactor,
        };
    }

    protected virtual JsonArray BuildDisplays()
    {
        var dd = DisplayDevice.All.ToList();
        var items = new List<JsonNode?>();
        foreach (var path in DisplayConfig.Query())
        {
            var tar = DisplayConfig.GetTargetName(path);
            var src = DisplayConfig.GetSourceName(path);
            var display = dd.FirstOrDefault(m => m.DeviceName.EqualsIgnoreCase(src.viewGdiDeviceName.ToString()));
            if (display == null)
                continue;

            var obj = new JsonObject
            {
                ["monitorFriendlyDeviceName"] = tar.monitorFriendlyDeviceName.ToString(),
                ["edidManufactureId"] = tar.edidManufactureId,
                ["edidProductCodeId"] = tar.edidProductCodeId,
                ["connectorInstance"] = tar.connectorInstance,
                ["outputTechnology"] = tar.outputTechnology.ToString(),
                ["deviceName"] = display.DeviceName,
                ["deviceString"] = display.DeviceString,
                ["deviceStateFlags"] = display.StateFlags.ToString(),
                // ["deviceID"] = display.DeviceID,
                // ["deviceKey"] = display.DeviceKey,
            };
            items.Add(obj);

            if (display.IsPrimary)
            {
                obj["isPrimary"] = true;
            }

            var mon = display.Monitor;
            if (mon != null)
            {
                obj["bounds"] = new JsonObject
                {
                    ["left"] = mon.Bounds.left,
                    ["top"] = mon.Bounds.top,
                    ["right"] = mon.Bounds.right,
                    ["bottom"] = mon.Bounds.bottom,
                };

                obj["workingArea"] = new JsonObject
                {
                    ["left"] = mon.WorkingArea.left,
                    ["top"] = mon.WorkingArea.top,
                    ["right"] = mon.WorkingArea.right,
                    ["bottom"] = mon.WorkingArea.bottom,
                };

                obj["dpi"] = new JsonObject
                {
                    ["angular"] = new JsonObject
                    {
                        ["width"] = mon.AngularDpi.width,
                        ["height"] = mon.AngularDpi.height,
                    },
                    ["effective"] = new JsonObject
                    {
                        ["width"] = mon.EffectiveDpi.width,
                        ["height"] = mon.EffectiveDpi.height,
                    },
                    ["raw"] = new JsonObject
                    {
                        ["width"] = mon.RawDpi.width,
                        ["height"] = mon.RawDpi.height,
                    },
                };
            }
        }

        return new JsonArray([.. items]);
    }

    protected virtual JsonArray BuildAdapters()
    {
        var items = new List<JsonNode?>();
        try
        {
            using var factory = DXGIFunctions.CreateDXGIFactory1();
            var adapters = factory.EnumAdapters1().ToArray();
            try
            {
                foreach (var adapter in adapters)
                {
                    var desc = adapter.GetDesc();
                    var obj = new JsonObject
                    {
                        ["description"] = desc.Description.ToString(),
                        ["vendorId"] = desc.VendorId,
                        ["deviceId"] = desc.DeviceId,
                        ["dedicatedVideoMemory"] = desc.DedicatedVideoMemory,
                        ["sharedSystemMemory"] = desc.SharedSystemMemory,
                    };

                    if (desc.DeviceId == _warpDeviceId)
                    {
                        obj["isWarp"] = true;
                    }
                    items.Add(obj);
                }
            }
            finally
            {
                adapters.Dispose();
            }
        }
        catch (Exception ex)
        {
            // a machine with no usable DXGI (some server/session configurations) is not a reason to fail.
            AOTrinoApplication.Current?.TraceWarning($"Cannot enumerate graphics adapters: {ex.Message}");
        }
        return new JsonArray([.. items]);
    }

    // best-effort, from what the firmware says about itself. absent means "nothing detected", not "bare metal".
    public static string? GetVirtualMachine()
    {
        try
        {
            if (IsWindowsSandbox())
                return "Windows Sandbox";

            var manufacturer = GetSystemFirmwareString("SystemManufacturer");
            var product = GetSystemFirmwareString("SystemProductName");
            var both = $"{manufacturer} {product}".Trim();
            if (both.Length == 0)
                return null;

            foreach (var known in _virtualMachineMarkers)
            {
                if (both.Contains(known, StringComparison.OrdinalIgnoreCase))
                    return known;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsWindowsSandbox()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Edge\WindowsSandbox", false);
        if (key == null)
            return false;

        return key.GetValue("SandboxEnabled") is int i && i > 0;
    }

    // https://learn.microsoft.com/windows/win32/direct3ddxgi/d3d10-graphics-programming-guide-dxgi#new-info-about-enumerating-adapters-for-windows-8
    private const int _warpDeviceId = 0x8c;
    private static readonly string[] _virtualMachineMarkers = ["VMware", "VirtualBox", "Virtual Machine", "Hyper-V", "QEMU", "KVM", "Xen", "Parallels", "Bochs"];

    private static string GetSystemFirmwareString(string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS", false);
        return key?.GetValue(name) as string ?? string.Empty;
    }

    public override string ToString() => Values.ToJsonString();
}
