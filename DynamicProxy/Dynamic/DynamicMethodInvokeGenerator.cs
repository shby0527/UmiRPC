using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using Umi.Proxy.Dynamic.Dynamic.DynamicCall;

namespace Umi.Proxy.Dynamic.Dynamic;

public static class DynamicMethodInvokeGenerator
{
    private static readonly IDictionary<MethodInfo, Func<object, object[], object>> InstanceMethodCache;

    private static readonly IDictionary<MethodInfo, Func<object[], object>> StaticMethodCache;

    private static readonly IDictionary<MethodInfo, IInstanceMethodCaller> InstanceMethodCallerCache;
    private static readonly IDictionary<MethodInfo, IStaticMethodCaller> StaticMethodCallers;

    private static readonly ConstructorInfo ArgumentOutOfRangExceptionConstructor;
    private static readonly ConstructorInfo NullReferenceExceptionConstructor;

    private static readonly ModuleBuilder ModuleBuilder;

    private static readonly MethodInfo InstanceMethodCallMethod;
    private static readonly MethodInfo StaticMethodCallMethod;

    private static readonly ConstructorInfo ObjectConstructor;

    static DynamicMethodInvokeGenerator()
    {
        InstanceMethodCache = new ConcurrentDictionary<MethodInfo, Func<object, object[], object>>();
        StaticMethodCache = new ConcurrentDictionary<MethodInfo, Func<object[], object>>();
        InstanceMethodCallerCache = new ConcurrentDictionary<MethodInfo, IInstanceMethodCaller>();
        StaticMethodCallers = new ConcurrentDictionary<MethodInfo, IStaticMethodCaller>();
        InstanceMethodCallMethod = typeof(IInstanceMethodCaller)
                                       .GetMethod(nameof(IInstanceMethodCaller.Call),
                                           [typeof(object), typeof(object[])])
                                   ?? throw new InvalidOperationException("important method not found");
        ObjectConstructor = typeof(object).GetConstructor(Type.EmptyTypes)
                            ?? throw new InvalidOperationException("important method not found");
        StaticMethodCallMethod = typeof(IStaticMethodCaller)
                                     .GetMethod(nameof(IStaticMethodCaller.Call), [typeof(object[])])
                                 ?? throw new InvalidOperationException("important method not found");
        ArgumentOutOfRangExceptionConstructor = typeof(ArgumentOutOfRangeException)
                                                    .GetConstructor([typeof(string)])
                                                ?? throw new InvalidOperationException("constructor( not found");
        NullReferenceExceptionConstructor = typeof(NullReferenceException)
                                                .GetConstructor([typeof(string)])
                                            ?? throw new InvalidOperationException("constructor( not found");
        var assemblyName = new AssemblyName("DynamicMethodCallers");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder = assemblyBuilder.DefineDynamicModule("DynamicMethodCallers.dll");
    }

    // 静态的
    public static Func<object[], object> GenerateStaticMethod(MethodInfo methodInfo)
    {
        if (methodInfo is { IsStatic: false }
            or { IsGenericMethodDefinition: true }
            or { IsAbstract: true }
            or { DeclaringType: null })
            throw new NotSupportedException($"{nameof(methodInfo)} are not supported");
        if (methodInfo.DeclaringType is { IsGenericTypeDefinition: true })
            throw new InvalidOperationException("declare MUST close type");

        return StaticMethodCache.GetOrDefault(methodInfo, CreateStaticMethod);

        Func<object[], object> CreateStaticMethod()
        {
            DynamicMethod method = new("", typeof(object), [typeof(object[])]);
            var il = method.GetILGenerator();
            // 这个方法主要职责就是, 先检查 object 数组长度，不满足就异常, 然后生成调用代码,
            /*
             *  C# 代码
             * if(arg1.Length < methodInfo.GetParameters().Length) throw new ArgumentOutOfRangException("argument");
             * return arg0.MethodInfo(arg1[0], arg1[1]……);
             */
            var throwOutIndex = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Ldc_I4, methodInfo.GetParameters().Length);
            il.Emit(OpCodes.Blt_Un, throwOutIndex);

            // load this
            var parameters = methodInfo.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4_S, i);
                il.Emit(OpCodes.Ldelem_Ref);
                if (parameters[i].ParameterType is { IsValueType: true })
                    il.Emit(OpCodes.Unbox_Any, parameters[i].ParameterType);
            }

            il.Emit(OpCodes.Call, methodInfo);
            if (methodInfo.ReturnType == typeof(void))
                il.Emit(OpCodes.Ldnull);
            if (methodInfo.ReturnType is { IsValueType: true })
                il.Emit(OpCodes.Box, methodInfo.ReturnType);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(throwOutIndex);
            il.Emit(OpCodes.Ldstr, "arguments count not enough");
            il.Emit(OpCodes.Newobj, ArgumentOutOfRangExceptionConstructor);
            il.Emit(OpCodes.Throw);
            return method.CreateDelegate<Func<object[], object>>();
        }
    }

    //实例的
    public static Func<object, object[], object> GenerateInstanceMethod(MethodInfo methodInfo)
    {
        if (methodInfo is { IsStatic: true }
            or { IsGenericMethodDefinition: true }
            or { IsAbstract: true }
            or { DeclaringType: null })
            throw new NotSupportedException($"{nameof(methodInfo)} are not supported");
        if (methodInfo.DeclaringType is { IsGenericTypeDefinition: true })
            throw new InvalidOperationException("declare MUST close type");

        return InstanceMethodCache.GetOrDefault(methodInfo, InterGenerateMethod);

        Func<object, object[], object> InterGenerateMethod()
        {
            DynamicMethod method = new("", typeof(object), [typeof(object), typeof(object[])]);
            var il = method.GetILGenerator();
            // 这个方法主要职责就是, 先检查 object 数组长度，不满足就异常, 然后生成调用代码,还要判断一下 arg0 是不是 null ，以及
            // object.GetType() 是不是 typeof(methodInfo.DeclaringType) 除非它是 null ?
            /*
             *  C# 代码
             * if(arg1.Length < methodInfo.GetParameters().Length) throw new ArgumentOutOfRangException("argument");
             * if(arg0 == null) throw new NullReferenceException();
             * if(arg0.GetType() != methodInfo.DeclaringType) throw new InvalidOperationException();
             * return arg0.MethodInfo(arg1[0], arg1[1]……);
             */
            var throwNullLabel = il.DefineLabel();
            var throwOutIndex = il.DefineLabel();
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Beq, throwNullLabel); // if null == arg0 jmp ---> throwNullLabel
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Ldc_I4, methodInfo.GetParameters().Length);
            il.Emit(OpCodes.Blt_Un, throwOutIndex);

            // load this
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, methodInfo.DeclaringType);
            var parameters = methodInfo.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4_S, i);
                il.Emit(OpCodes.Ldelem_Ref);
                if (parameters[i].ParameterType is { IsValueType: true })
                    il.Emit(OpCodes.Unbox_Any, parameters[i].ParameterType);
            }

            il.Emit(OpCodes.Callvirt, methodInfo);
            if (methodInfo.ReturnType == typeof(void))
                il.Emit(OpCodes.Ldnull);
            if (methodInfo.ReturnType is { IsValueType: true })
                il.Emit(OpCodes.Box, methodInfo.ReturnType);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(throwNullLabel);
            il.Emit(OpCodes.Ldstr, "instance object is null");
            il.Emit(OpCodes.Newobj, NullReferenceExceptionConstructor);
            il.Emit(OpCodes.Throw);
            il.MarkLabel(throwOutIndex);
            il.Emit(OpCodes.Ldstr, "arguments count not enough");
            il.Emit(OpCodes.Newobj, ArgumentOutOfRangExceptionConstructor);
            il.Emit(OpCodes.Throw);
            return method.CreateDelegate<Func<object, object[], object>>();
        }
    }

    public static IStaticMethodCaller GenerateStaticMethodCaller(MethodInfo methodInfo)
    {
        if (methodInfo is { IsStatic: false }
            or { IsGenericMethodDefinition: true }
            or { IsAbstract: true }
            or { DeclaringType: null })
            throw new NotSupportedException($"{nameof(methodInfo)} are not supported");
        if (methodInfo.DeclaringType is { IsGenericTypeDefinition: true })
            throw new InvalidOperationException("declare MUST close type");

        IStaticMethodCaller Create()
        {
            var typeBuilder = ModuleBuilder.DefineType(
                $"{AssemblyGenerator.NameSpacePrefix}.${methodInfo.DeclaringType.Name}${methodInfo.Name}$Static$Caller",
                TypeAttributes.Public | TypeAttributes.Sealed);
            typeBuilder.AddInterfaceImplementation(typeof(IStaticMethodCaller));
            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig,
                CallingConventions.Standard | CallingConventions.HasThis, null);
            var il = constructorBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, ObjectConstructor);
            il.Emit(OpCodes.Ret);
            constructorBuilder.SetImplementationFlags(MethodImplAttributes.IL);
            var methodBuilder = typeBuilder.DefineMethod(StaticMethodCallMethod.Name,
                StaticMethodCallMethod.Attributes ^ MethodAttributes.Abstract,
                StaticMethodCallMethod.CallingConvention,
                typeof(object), [typeof(object[])]);
            typeBuilder.DefineMethodOverride(methodBuilder, StaticMethodCallMethod);
            il = methodBuilder.GetILGenerator();

            var throwOutIndex = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Ldc_I4, methodInfo.GetParameters().Length);
            il.Emit(OpCodes.Blt_Un, throwOutIndex);

            // load this
            var parameters = methodInfo.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4_S, i);
                il.Emit(OpCodes.Ldelem_Ref);
                if (parameters[i].ParameterType is { IsValueType: true })
                    il.Emit(OpCodes.Unbox_Any, parameters[i].ParameterType);
            }

            il.Emit(OpCodes.Call, methodInfo);
            if (methodInfo.ReturnType == typeof(void))
                il.Emit(OpCodes.Ldnull);
            if (methodInfo.ReturnType is { IsValueType: true })
                il.Emit(OpCodes.Box, methodInfo.ReturnType);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(throwOutIndex);
            il.Emit(OpCodes.Ldstr, "arguments count not enough");
            il.Emit(OpCodes.Newobj, ArgumentOutOfRangExceptionConstructor);
            il.Emit(OpCodes.Throw);
            methodBuilder.SetImplementationFlags(MethodImplAttributes.IL);

            var typeInfo = typeBuilder.CreateTypeInfo();
            return Activator.CreateInstance(typeInfo) as IStaticMethodCaller
                   ?? throw new NotSupportedException($"{nameof(IStaticMethodCaller)} is not supported");
        }

        return StaticMethodCallers.GetOrDefault(methodInfo, Create);
    }

    public static IInstanceMethodCaller GenerateInstanceMethodCaller(MethodInfo methodInfo)
    {
        if (methodInfo is { IsStatic: true }
            or { IsGenericMethodDefinition: true }
            or { IsAbstract: true }
            or { DeclaringType: null })
            throw new NotSupportedException($"{nameof(methodInfo)} are not supported");
        if (methodInfo.DeclaringType is { IsGenericTypeDefinition: true })
            throw new InvalidOperationException("declare MUST close type");


        return InstanceMethodCallerCache.GetOrDefault(methodInfo, Create);

        IInstanceMethodCaller Create()
        {
            var typeBuilder = ModuleBuilder.DefineType(
                $"{AssemblyGenerator.NameSpacePrefix}.${methodInfo.DeclaringType.Name}${methodInfo.Name}$Instance$Caller",
                TypeAttributes.Public | TypeAttributes.Sealed);
            typeBuilder.AddInterfaceImplementation(typeof(IInstanceMethodCaller));
            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig,
                CallingConventions.Standard | CallingConventions.HasThis, null);
            var il = constructorBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, ObjectConstructor);
            il.Emit(OpCodes.Ret);
            constructorBuilder.SetImplementationFlags(MethodImplAttributes.IL);
            var methodBuilder = typeBuilder.DefineMethod(InstanceMethodCallMethod.Name,
                InstanceMethodCallMethod.Attributes ^ MethodAttributes.Abstract,
                InstanceMethodCallMethod.CallingConvention,
                typeof(object), [typeof(object), typeof(object[])]);
            typeBuilder.DefineMethodOverride(methodBuilder, InstanceMethodCallMethod);
            il = methodBuilder.GetILGenerator();

            var throwNullLabel = il.DefineLabel();
            var throwOutIndex = il.DefineLabel();
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Beq, throwNullLabel); // if null == arg0 jmp ---> throwNullLabel
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Ldc_I4, methodInfo.GetParameters().Length);
            il.Emit(OpCodes.Blt_Un, throwOutIndex);

            // load this
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, methodInfo.DeclaringType);
            var parameters = methodInfo.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldc_I4_S, i);
                il.Emit(OpCodes.Ldelem_Ref);
                if (parameters[i].ParameterType is { IsValueType: true })
                    il.Emit(OpCodes.Unbox_Any, parameters[i].ParameterType);
            }

            il.Emit(OpCodes.Callvirt, methodInfo);
            if (methodInfo.ReturnType == typeof(void))
                il.Emit(OpCodes.Ldnull);
            if (methodInfo.ReturnType is { IsValueType: true })
                il.Emit(OpCodes.Box, methodInfo.ReturnType);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(throwNullLabel);
            il.Emit(OpCodes.Ldstr, "instance object is null");
            il.Emit(OpCodes.Newobj, NullReferenceExceptionConstructor);
            il.Emit(OpCodes.Throw);
            il.MarkLabel(throwOutIndex);
            il.Emit(OpCodes.Ldstr, "arguments count not enough");
            il.Emit(OpCodes.Newobj, ArgumentOutOfRangExceptionConstructor);
            il.Emit(OpCodes.Throw);

            methodBuilder.SetImplementationFlags(MethodImplAttributes.IL);

            var typeInfo = typeBuilder.CreateTypeInfo();
            return Activator.CreateInstance(typeInfo) as IInstanceMethodCaller
                   ?? throw new NotSupportedException($"{nameof(IInstanceMethodCaller)} is not supported");
        }
    }
}