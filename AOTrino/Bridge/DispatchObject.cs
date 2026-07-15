namespace AOTrino.Bridge;

// A generic base host object for the WebView2 control must implement IDispatch
// when we have say, this in javascript:
//
//         var options = JSON.parse(chrome.webview.hostObjects.dotnet.getInfo());
//
// 'dotnet' corresponds to this instance, and the WebView2 runtime will
//
// 1) call "dotnet" asking for a "getInfo" method or property
// 2) call this returned object's to do the function call (with 0 or more parameters). so only Invoke(0) should be called on this
//
// that's why we have two implementations of IDispatch here
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)]
public partial class DispatchObject : IDispatch
{
    private static readonly ConcurrentDictionary<Type, DispatchType> _cache = new();
    private const int _dispidBase = 1000;
    private static readonly string[] _reservedJavascriptNames =
    [
        "name", "constructor", "prototype", "toString", "valueOf", "hasOwnProperty", "isPrototypeOf", "propertyIsEnumerable", "toLocaleString"
    ];

    private readonly ConcurrentDictionary<string, object?> _customValues = new(StringComparer.OrdinalIgnoreCase);

    private object? GetCustomValue(string name)
    {
        _customValues.TryGetValue(name, out var value);
        return value;
    }

    public virtual void SetCustomValue(string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        _customValues[name] = value;

        var type = GetType();
        if (!_cache.TryGetValue(type, out var dispatchType))
        {
            dispatchType = new DispatchType(type);
            dispatchType.SetCustomValueGetter(name, typeof(DispatchObject).GetMethod(nameof(GetCustomValue), BindingFlags.Instance | BindingFlags.NonPublic)!);
        }
    }

    // unwraps the value of an async host method so it can cross to JS.
    // a switch rather than reflection over Task<T>.Result or a dynamic cast, because those need types the AOT
    // compiler cannot see coming and the bridge has to survive trimming.
    // the well-known list below covers what JS can actually receive, so most host objects need nothing at all.
    // for a Task<T> of your own, override and chain to base (see docs/BRIDGE.md):
    //
    //   protected override object? GetTaskResult(Task task) => task switch
    //   {
    //       Task<MyThing> t => t.Result,
    //       _ => base.GetTaskResult(task),
    //   };
    protected virtual object? GetTaskResult(Task task) => task switch
    {
        Task<string> t => t.Result,
        Task<bool> t => t.Result,
        Task<int> t => t.Result,
        Task<long> t => t.Result,
        Task<short> t => t.Result,
        Task<byte> t => t.Result,
        Task<sbyte> t => t.Result,
        Task<uint> t => t.Result,
        Task<ulong> t => t.Result,
        Task<ushort> t => t.Result,
        Task<float> t => t.Result,
        Task<double> t => t.Result,
        Task<decimal> t => t.Result,
        Task<char> t => t.Result,
        Task<DateTime> t => t.Result,
        Task<DateTimeOffset> t => t.Result,
        Task<TimeSpan> t => t.Result,
        Task<Guid> t => t.Result,
        Task<Uri> t => t.Result,
        Task<object> t => t.Result,

        // arrays of the above cross as JS arrays
        Task<string[]> t => t.Result,
        Task<bool[]> t => t.Result,
        Task<int[]> t => t.Result,
        Task<long[]> t => t.Result,
        Task<double[]> t => t.Result,
        Task<float[]> t => t.Result,
        Task<object[]> t => t.Result,

        // nullables, for a host method that returns Task<int?> and friends
        Task<bool?> t => t.Result,
        Task<int?> t => t.Result,
        Task<long?> t => t.Result,
        Task<double?> t => t.Result,
        Task<Guid?> t => t.Result,
        Task<DateTime?> t => t.Result,

        _ => throw new NotSupportedException($"Type '{GetType().FullName}' returns a task of type '{task.GetType().FullName}'. Override {nameof(GetTaskResult)} to unwrap it; see docs/BRIDGE.md."),
    };

    protected virtual TaskFunction CreateTaskFunction(MethodInfo method, object?[]? arguments) => new(this, method, arguments);
    protected virtual bool TryConvertArgument(MethodInfo method, int index, ParameterInfo parameter, object? value, out object? converted) =>
        Conversions.TryChangeObjectType(value, parameter.ParameterType, out converted);

    // when working with WebView2 and the HostObjectHelper is installed, set this to true
    public static bool ContinueOnAsync { get; set; }

    // when working with WebView2 and the HostObjectHelper is installed, set this to true
    public static bool OneStepInvoke { get; set; }

    // hides a member from JS without deleting it. [Browsable(false)] does the same at compile time when you
    // own the class; this is for when you don't - dropping a property from a host object you inherited, say
    // AOTrino's SystemInfo:
    //
    //   protected override bool IsMemberVisible(string name) =>
    //       name != nameof(SystemInfo.Adapters) && base.IsMemberVisible(name);
    //
    // adding members needs nothing: the dispatch cache is per runtime type, so a subclass's own public
    // members show up on their own.
    protected virtual bool IsMemberVisible(string name) => true;

    public virtual bool IsMethod(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return true;

        var type = GetType();
        if (!_cache.TryGetValue(type, out var dispatchType))
        {
            dispatchType = new DispatchType(type);
            _cache[type] = dispatchType;
        }

        return dispatchType.IsMethod(name) == true;
    }

    public virtual bool IsAsync(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return true;

        var type = GetType();
        if (!_cache.TryGetValue(type, out var dispatchType))
        {
            dispatchType = new DispatchType(type);
            _cache[type] = dispatchType;
        }

        return dispatchType.IsAsync(name) == true;
    }

    HRESULT IDispatch.GetIDsOfNames(in Guid riid, PWSTR[] rgszNames, uint cNames, uint lcid, int[] rgDispId) =>
        GetIDsOfNames(in riid, rgszNames, cNames, lcid, rgDispId);

    protected virtual HRESULT GetIDsOfNames(in Guid riid, PWSTR[] rgszNames, uint cNames, uint lcid, int[] rgDispId)
    {
        if (rgszNames == null || rgszNames.Length == 0 || rgszNames.Length != cNames)
            return DirectNConstants.E_INVALIDARG;

        var type = GetType();
        for (var i = 0; i < cNames; i++)
        {
            var name = rgszNames[i].ToString();
            if (name == null)
            {
                rgDispId[i] = -1;
                continue;
            }

            //Application.TraceVerbose($"looking for '{name}' in '{type.FullName}'");
            if (!_cache.TryGetValue(type, out var dispatchType))
            {
                dispatchType = new DispatchType(type);
                _cache[type] = dispatchType;
            }

            var dispId = dispatchType.GetDispId(name);
            if (dispId >= 0 && !IsMemberVisible(name))
            {
                // hidden on purpose, so it isn't an error worth tracing: JS just doesn't see the member
                dispId = -1;
            }
            else if (dispId < 0)
            {
                Application.TraceError($"DISP_E_UNKNOWNNAME {GetType().FullName} {name}");
            }

            rgDispId[i] = dispId < 0 ? -1 : dispId + _dispidBase;
        }

        if (rgDispId.Any(id => id == -1))
        {
            const int DISP_E_UNKNOWNNAME = unchecked((int)0x80020006);
            return DISP_E_UNKNOWNNAME;
        }

        //Application.TraceVerbose($"S_OK");
        return DirectNConstants.S_OK;
    }

    HRESULT IDispatch.Invoke(int dispIdMember, in Guid riid, uint lcid, DISPATCH_FLAGS wFlags, in DISPPARAMS pDispParams, nint pVarResult, nint pExcepInfo, nint puArgErr) =>
        Invoke(dispIdMember, riid, lcid, wFlags, pDispParams, pVarResult, pExcepInfo, puArgErr);

    protected virtual unsafe HRESULT Invoke(int dispIdMember, in Guid riid, uint lcid, DISPATCH_FLAGS wFlags, in DISPPARAMS pDispParams, nint pVarResult, nint pExcepInfo, nint puArgErr)
    {
        try
        {
            var type = GetType();
            if (!_cache.TryGetValue(type, out var dispatchType) || dispatchType.GetMethod(dispIdMember - _dispidBase) is not MethodInfo method || method == null)
            {
                Application.TraceError($"DISP_E_MEMBERNOTFOUND {GetType().FullName} {dispIdMember}");
                const int DISP_E_MEMBERNOTFOUND = unchecked((int)0x80020003);
                return DISP_E_MEMBERNOTFOUND;
            }

            var isAsync = ContinueOnAsync && DispatchType.IsAsync(method);
            if (isAsync)
            {
                var tf = CreateTaskFunction(method, Function.BuildArguments(this, method, pDispParams));
                using var vatf = new Variant(tf);
                vatf.DetachTo(pVarResult);
                return DirectNConstants.S_OK;
            }

            if (OneStepInvoke)
            {
                var arguments = Function.BuildArguments(this, method, pDispParams);
                var result = method.Invoke(this, arguments);
                return Function.WriteResultAsVARIANT(result, pVarResult);
            }

            var func = new Function(this, method);
            using var va = new Variant(func);
            va.DetachTo(pVarResult);
            return DirectNConstants.S_OK;
        }
        catch (Exception ex)
        {
            // a host method exception is handed back to JS below (via EXCEPINFO).
            // it's control flow, not an app error to report
            Application.TraceWarning($"Host object method exception: {ex.Message}");
            if (pExcepInfo != 0)
            {
                var excepInfo = new EXCEPINFO
                {
                    bstrSource = new(Marshal.StringToBSTR(GetType().FullName)),
                    scode = ex.HResult,
                    bstrDescription = new(Marshal.StringToBSTR(ex.ToString())),
                };

                *(EXCEPINFO*)pExcepInfo = excepInfo;
            }
            return DirectNConstants.E_FAIL;
        }
    }

    HRESULT IDispatch.GetTypeInfo(uint iTInfo, uint lcid, out ITypeInfo ppTInfo) => throw new NotSupportedException();
    HRESULT IDispatch.GetTypeInfoCount(out uint pctinfo)
    {
        pctinfo = 0;
        return DirectNConstants.S_OK;
    }

    public static Window GetWindow()
    {
        var windows = Application.GetApplication(Environment.CurrentManagedThreadId)?.Windows;
        if (windows == null || windows.Count == 0)
            throw new InvalidAsynchronousStateException();

        var window = windows.FirstOrDefault(w => w.TaskScheduler != null && !w.IsBackground) ?? windows.FirstOrDefault(w => w.TaskScheduler != null);
        return window ?? throw new InvalidAsynchronousStateException();
    }

    [System.Runtime.InteropServices.Marshalling.GeneratedComClass]
    public partial class TaskFunction(DispatchObject obj, MethodInfo method, object?[]? arguments) : IUnknown
    {
        public virtual unsafe void Continue(Func<HRESULT, VARIANT, HRESULT> continuation)
        {
            ArgumentNullException.ThrowIfNull(continuation);
            try
            {
                var result = method.Invoke(obj, arguments);
                if (result is Task task) // it *should* be a task if we're here but just in case
                {
                    switch (task.Status)
                    {
                        case TaskStatus.RanToCompletion:
                            break;

                        case TaskStatus.Canceled:
                            break;

                        case TaskStatus.Faulted:
                            break;

                        case TaskStatus.Created:
                        case TaskStatus.WaitingForActivation:
                        case TaskStatus.WaitingToRun:
                        case TaskStatus.Running:
                        case TaskStatus.WaitingForChildrenToComplete:
                            var awaiter = task.GetAwaiter();
                            awaiter.OnCompleted(() =>
                            {
                                try
                                {
                                    result = obj.GetTaskResult(task);
                                    var v = new VARIANT();
                                    var hr = Function.WriteResultAsVARIANT(result, (nint)(&v));
                                    continuation.Invoke(hr, v).ThrowOnError();
                                }
                                catch (Exception ex)
                                {
                                    Application.TraceError($"OnCompleted Exception: {ex}");
                                }
                            });
                            return;
                    }

                    result = obj.GetTaskResult(task);
                }

                var v = new VARIANT();
                var hr = Function.WriteResultAsVARIANT(result, (nint)(&v));
                continuation.Invoke(hr, v).ThrowOnError();
            }
            catch (Exception ex)
            {
                Application.TraceError($"Exception: {ex}");
                continuation.Invoke(ex.HResult, new VARIANT()).ThrowOnError();
            }
        }
    }

    private sealed class DispatchType
    {
        private readonly Dictionary<string, Dispid> _methods = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, MethodInfo> _getMethods = [];

        public DispatchType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                if (method.Attributes.HasFlag(MethodAttributes.SpecialName)) // like get_ / set_ / add_ / remove_ / etc.
                    continue;

                var browsable = method.GetCustomAttribute<BrowsableAttribute>()?.Browsable;
                if (browsable.HasValue && !browsable.Value)
                    continue;

                checkName(method.Name);
                var id = new Dispid
                {
                    Id = _methods.Count,
                    IsMethod = true,
                    IsAsync = IsAsync(method)
                };

                // note we don't support overloaded methods
                _methods[method.Name] = id;
                _getMethods[id.Id] = method;

                //Application.TraceVerbose($"Type '{type.FullName}' method '{method.Name}' id: {_dispidBase + id}");
            }

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];

                var browsable = property.GetCustomAttribute<BrowsableAttribute>()?.Browsable;
                if (browsable.HasValue && !browsable.Value)
                    continue;

                var getMethod = property.GetGetMethod();
                if (getMethod == null)
                    continue;

                checkName(property.Name);
                var id = new Dispid { Id = _methods.Count, IsMethod = false };
                _methods[property.Name] = id;
                _getMethods[id.Id] = getMethod;

                //Application.TraceVerbose($"Type '{type.FullName}' get '{property.Name}' id: {_dispidBase + id}");
            }

            void checkName(string name)
            {
                if (name == nameof(ToString)) // don't warn
                    return;

                if (_reservedJavascriptNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    Application.TraceWarning($"The property or method name '{name}' of '{type.FullName}' type is reserved by JavaScript and will never be used.");
                }
            }
        }

        public void SetCustomValueGetter(string name, MethodInfo getCustomValue)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(getCustomValue);
            if (_methods.ContainsKey(name))
                throw new ArgumentException($"Method or property '{name}' is already defined.");

            var id = new Dispid
            {
                Id = _methods.Count,
                IsMethod = true,
            };
            _methods[name] = id;
            _getMethods[id.Id] = getCustomValue;
        }

        public MethodInfo? GetMethod(int dispId)
        {
            _getMethods.TryGetValue(dispId, out var method);
            return method;
        }

        public int GetDispId(string name)
        {
            if (!_methods.TryGetValue(name, out var dispId))
                return -1;

            return dispId.Id;
        }

        public bool? IsMethod(string name)
        {
            if (!_methods.TryGetValue(name, out var dispId))
                return false;

            return dispId.IsMethod;
        }

        public bool? IsAsync(string name)
        {
            if (!_methods.TryGetValue(name, out var dispId))
                return false;

            return dispId.IsAsync;
        }

        private struct Dispid
        {
            public int Id;
            public bool IsMethod;
            public bool IsAsync;
        }

        public static bool IsAsync(MethodInfo method) => typeof(Task).IsAssignableFrom(method.ReturnType);
    }

    [System.Runtime.InteropServices.Marshalling.GeneratedComClass]
    private partial class Function(DispatchObject obj, MethodInfo method) : IDispatch
    {
#pragma warning disable IDE1006 // Naming Styles
        private const uint WM_COMPLETED = MessageDecoder.WM_APP + 1234;
#pragma warning restore IDE1006 // Naming Styles

        public HRESULT GetTypeInfo(uint iTInfo, uint lcid, out ITypeInfo ppTInfo) => throw new NotSupportedException();
        public HRESULT GetTypeInfoCount(out uint pctinfo)
        {
            pctinfo = 0;
            return DirectNConstants.S_OK;
        }

        public static unsafe object?[]? BuildArguments(DispatchObject obj, MethodInfo method, in DISPPARAMS parameters)
        {
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(method);

            var arguments = method.GetParameters();
            if (arguments.Length == 0)
                return null;

            var varArgs = (VARIANT*)parameters.rgvarg;
            var args = new object?[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                object? value;
                // note arguments are stored in in reverse order
                var varArgsIndex = parameters.cArgs - i - 1;
                if (varArgsIndex < 0 || varArgsIndex >= parameters.cArgs)
                {
                    value = null;
                }
                else
                {
                    value = Variant.Unwrap(varArgs[varArgsIndex]);
                }

                if (obj.TryConvertArgument(method, i, arguments[i], value, out var converted))
                {
                    value = converted;
                }

                args[i] = value;
            }
            return args;
        }

        public static unsafe HRESULT WriteResultAsVARIANT(object? result, nint pVarResult)
        {
            if (result is Variant variant)
            {
                variant.DetachTo(pVarResult);
                return DirectNConstants.S_OK;
            }

            if (result is VARIANT v)
            {
                *(VARIANT*)pVarResult = v;
                return DirectNConstants.S_OK;
            }

            if (result is IDictionary dictionary)
            {
                var array = new List<object>();
                foreach (DictionaryEntry kv in dictionary)
                {
                    var dkv = new DispatchKeyValue { Key = kv.Key, Value = kv.Value };
                    array.Add(dkv);
                }
                result = array.ToArray();
            }
            else if (result is Version)
            {
                result = result.ToString();
            }

            using var va = new Variant(result);
            va.DetachTo(pVarResult);
            return DirectNConstants.S_OK;
        }

        public HRESULT GetIDsOfNames(in Guid riid, PWSTR[] rgszNames, uint cNames, uint lcid, int[] rgDispId)
        {
            if (rgszNames == null || rgszNames.Length == 0 || rgszNames.Length != cNames)
                return DirectNConstants.E_INVALIDARG;

            // we need to invoke first
            var target = (DispatchObject)method.Invoke(obj, null)!;
            return target.GetIDsOfNames(riid, rgszNames, cNames, lcid, rgDispId);
        }

        public unsafe HRESULT Invoke(int dispIdMember, in Guid riid, uint lcid, DISPATCH_FLAGS wFlags, in DISPPARAMS pDispParams, nint pVarResult, nint pExcepInfo, nint puArgErr)
        {
            try
            {
                //Application.TraceVerbose($"Function dispIdMember '{dispIdMember}'");
                if (dispIdMember != 0)
                {
                    // we need to invoke first
                    var target = (DispatchObject)method.Invoke(obj, null)!;
                    return target.Invoke(dispIdMember, riid, lcid, wFlags, pDispParams, pVarResult, pExcepInfo, puArgErr);
                }

                var args = BuildArguments(obj, method, pDispParams);
                var result = method.Invoke(obj, args);
                if (result is Task task)
                {
                    // if you are here it's because ContinueOnAsync is false
                    switch (task.Status)
                    {
                        case TaskStatus.RanToCompletion:
                            break;

                        case TaskStatus.Canceled:
                            break;

                        case TaskStatus.Faulted:
                            break;

                        case TaskStatus.Created:
                        case TaskStatus.WaitingForActivation:
                        case TaskStatus.WaitingToRun:
                        case TaskStatus.Running:
                        case TaskStatus.WaitingForChildrenToComplete:
                            // we need to run message loop, on the UI thread, until the task is completed
                            var window = GetWindow();

                            // note this code avoids using reflection on Task<T> to avoid trimming issues with AOT publishing
                            var completed = false;
                            var awaiter = task.GetAwaiter();
                            awaiter.OnCompleted(() =>
                            {
                                completed = true;
                                DirectNFunctions.PostMessageW(window.Handle, WM_COMPLETED, 0, 0);
                            });

                            // note if the window enters a modal loop (like moving it using the caption bar),
                            // the invoke call will not return until the modal loop is exited. Not sure how to avoid this...
                            var app = Application.Current;
                            if (app != null)
                            {
                                while (!completed)
                                {
                                    app.RunMessageLoop(msg => msg.message == WM_COMPLETED);
                                }
                            }
                            break;
                    }

                    result = obj.GetTaskResult(task);
                }

                return WriteResultAsVARIANT(result, pVarResult);
            }
            catch (Exception ex)
            {
                // a host method exception is handed back to JS below (via EXCEPINFO).
                // it's control flow, not an app error to report
                Application.TraceWarning($"Host object method exception: {ex.Message}");
                if (pExcepInfo != 0)
                {
                    var excepInfo = new EXCEPINFO
                    {
                        bstrSource = new(Marshal.StringToBSTR(GetType().FullName)),
                        scode = ex.HResult,
                        bstrDescription = new(Marshal.StringToBSTR(ex.ToString())),
                    };

                    *(EXCEPINFO*)pExcepInfo = excepInfo;
                }
                return DirectNConstants.E_FAIL;
            }
        }
    }
}