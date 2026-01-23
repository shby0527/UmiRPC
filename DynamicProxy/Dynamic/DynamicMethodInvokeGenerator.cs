using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace Umi.Proxy.Dynamic.Dynamic;

public static class DynamicMethodInvokeGenerator
{
    private static readonly IDictionary<MethodInfo, Func<object, object[], object>> InstanceMethodCache;

    private static readonly IDictionary<MethodInfo, Func<object[], object>> StaticMethodCache;

    private static readonly MethodInfo TypeOpEquals;
    private static readonly MethodInfo ObjectGetTypeMethod;
    private static readonly MethodInfo TypeGetTypeFromHandle;

    private static readonly ConstructorInfo ArgumentOutOfRangExceptionConstructor;
    private static readonly ConstructorInfo NullReferenceExceptionConstructor;
    private static readonly ConstructorInfo InvalidOperationExceptionConstructor;

    static DynamicMethodInvokeGenerator()
    {
        InstanceMethodCache = new ConcurrentDictionary<MethodInfo, Func<object, object[], object>>();
        StaticMethodCache = new ConcurrentDictionary<MethodInfo, Func<object[], object>>();
        TypeOpEquals = typeof(Type).GetMethod("op_Equality",
                           BindingFlags.Static | BindingFlags.Public,
                           [typeof(Type), typeof(Type)])
                       ?? throw new InvalidOperationException("method not found");
        ObjectGetTypeMethod = typeof(object).GetMethod(nameof(GetType))
                              ?? throw new InvalidOperationException("method not found");
        TypeGetTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle),
                                    BindingFlags.Static | BindingFlags.Public,
                                    [typeof(RuntimeTypeHandle)])
                                ?? throw new InvalidOperationException("can not found helper method");
        ArgumentOutOfRangExceptionConstructor = typeof(ArgumentOutOfRangeException)
                                                    .GetConstructor([typeof(string)])
                                                ?? throw new InvalidOperationException("constructor( not found");
        NullReferenceExceptionConstructor = typeof(NullReferenceException)
                                                .GetConstructor([typeof(string)])
                                            ?? throw new InvalidOperationException("constructor( not found");

        InvalidOperationExceptionConstructor = typeof(InvalidOperationException)
                                                   .GetConstructor([typeof(string)])
                                               ?? throw new InvalidOperationException("constructor( not found");
    }

    public static Func<object, object[], object> GenerateInstanceMethod(MethodInfo methodInfo)
    {
        if (methodInfo is { IsStatic: true }
            or { IsGenericMethodDefinition: true }
            or { IsAbstract: true }
            or { DeclaringType: null })
            throw new NotSupportedException($"{nameof(methodInfo)} are not supported");

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
            var throwInvalidOp = il.DefineLabel();
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Beq, throwNullLabel); // if null == arg0 jmp ---> throwNullLabel
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Ldc_I4, methodInfo.GetParameters().Length);
            il.Emit(OpCodes.Blt_Un, throwOutIndex);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, ObjectGetTypeMethod);
            il.Emit(OpCodes.Ldtoken, methodInfo.DeclaringType);
            il.Emit(OpCodes.Call, TypeGetTypeFromHandle);
            il.Emit(OpCodes.Call, TypeOpEquals);
            il.Emit(OpCodes.Brfalse, throwInvalidOp);

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
            il.MarkLabel(throwInvalidOp);
            il.Emit(OpCodes.Ldstr, "type not match to object");
            il.Emit(OpCodes.Newobj, InvalidOperationExceptionConstructor);
            il.Emit(OpCodes.Throw);
            return method.CreateDelegate<Func<object, object[], object>>();
        }
    }
}