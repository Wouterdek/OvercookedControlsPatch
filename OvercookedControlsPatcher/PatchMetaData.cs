using System;
using System.Linq;
using Mono.Cecil;

namespace OvercookedControlsPatcher
{
    [AttributeUsage(System.AttributeTargets.Method)]
    public class AddMethod : System.Attribute
    {
        public string targetType;

        public AddMethod(string targetType)
        {
            this.targetType = targetType;
        }

        public static AddMethod Read(MethodDefinition method)
        {
            var attr = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == nameof(AddMethod));
            if (attr == null)
            {
                return null;
            }
            var targetType = (string)attr.ConstructorArguments[0].Value;
            AddMethod val = new AddMethod(targetType);
            return val;
        }
    }

    [AttributeUsage(System.AttributeTargets.Method)]
    public class ReplaceMethod : System.Attribute
    {
        public string targetType;

        public ReplaceMethod(string targetType)
        {
            this.targetType = targetType;
        }

        public static ReplaceMethod Read(MethodDefinition method)
        {
            var attr = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == nameof(ReplaceMethod));
            if (attr == null)
            {
                return null;
            }
            var targetType = (string)attr.ConstructorArguments[0].Value;
            ReplaceMethod val = new ReplaceMethod(targetType);
            return val;
        }
    }
}
