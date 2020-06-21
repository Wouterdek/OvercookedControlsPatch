using Mono.Cecil;
using Mono.Cecil.Cil;

namespace OvercookedControlsPatcher
{
    class PatchTools
    {
        /// Modified from https://groups.google.com/forum/#!msg/mono-cecil/uoMLJEZrQ1Q/ewthqjEk-jEJ
        /// <summary>
        /// Copy a method from one module to another.  If the same method exists in the target module, the caller
        /// is responsible to delete it first.
        /// The sourceMethod makes calls to other methods, we divide the calls into two types:
        /// 1. MethodDefinition : these are methods that are defined in the same module as the sourceMethod;
        /// 2. MethodReference : these are methods that are defined in a different module
        /// For type 1 calls, we will copy these MethodDefinitions to the same target typedef.
        /// For type 2 calls, we will not copy the called method
        /// 
        /// Another limitation: any TypeDefinitions that are used in the sourceMethod will not be copied to the target module; a 
        /// typereference is created instead.
        /// </summary>
        /// <param name="copyToTypedef">The typedef to copy the method to</param>
        /// <param name="sourceMethod">The method to copy</param>
        /// <returns></returns>
        public static MethodDefinition CopyMethod(TypeDefinition copyToTypedef, MethodDefinition sourceMethod)
        {

            ModuleDefinition targetModule = copyToTypedef.Module;

            // create a new MethodDefinition; all the content of sourceMethod will be copied to this new MethodDefinition

            MethodDefinition targetMethod = new MethodDefinition(sourceMethod.Name, sourceMethod.Attributes, targetModule.ImportReference(sourceMethod.ReturnType));


            // Copy the parameters; 
            foreach (ParameterDefinition p in sourceMethod.Parameters)
            {
                ParameterDefinition nP = new ParameterDefinition(p.Name, p.Attributes, targetModule.ImportReference(p.ParameterType));
                targetMethod.Parameters.Add(nP);
            }

            // copy the body
            MethodBody nBody = targetMethod.Body;
            MethodBody oldBody = sourceMethod.Body;

            nBody.InitLocals = oldBody.InitLocals;

            // copy the local variable definition
            foreach (VariableDefinition v in oldBody.Variables)
            {
                VariableDefinition nv = new VariableDefinition(targetModule.ImportReference(v.VariableType));
                //v.Name, 
                nBody.Variables.Add(nv);
            }

            // copy the IL; we only need to take care of reference and method definitions
            Mono.Collections.Generic.Collection<Instruction> col = nBody.Instructions;
            foreach (Instruction i in oldBody.Instructions)
            {
                object operand = i.Operand;
                if (operand == null)
                {
                    col.Add(Instruction.Create(i.OpCode));
                }

                // for any methodef that this method calls, we will copy it

                else if (operand is MethodDefinition)
                {
                    MethodDefinition dmethod = operand as MethodDefinition;
                    MethodDefinition newMethod = CopyMethod(copyToTypedef, dmethod);
                    col.Add(Instruction.Create(i.OpCode, newMethod));
                }

                // for member reference, import it
                else if (operand is FieldReference)
                {
                    FieldReference fref = operand as FieldReference;
                    FieldReference newf = targetModule.ImportReference(fref);
                    col.Add(Instruction.Create(i.OpCode, newf));
                }
                else if (operand is TypeReference)
                {
                    TypeReference tref = operand as TypeReference;
                    TypeReference newf = targetModule.ImportReference(tref);
                    col.Add(Instruction.Create(i.OpCode, newf));
                }
                else if (operand is TypeDefinition)
                {
                    TypeDefinition tdef = operand as TypeDefinition;
                    TypeReference newf = targetModule.ImportReference(tdef);
                    col.Add(Instruction.Create(i.OpCode, newf));
                }
                else if (operand is MethodReference)
                {
                    MethodReference mref = operand as MethodReference;
                    MethodReference newf = targetModule.ImportReference(mref);
                    col.Add(Instruction.Create(i.OpCode, newf));
                }
                else
                {
                    // we don't need to do any processing on the operand
                    col.Add(i);
                }
            }

            // copy the exception handler blocks

            foreach (ExceptionHandler eh in oldBody.ExceptionHandlers)
            {
                ExceptionHandler neh = new ExceptionHandler(eh.HandlerType);
                neh.CatchType = targetModule.ImportReference(eh.CatchType);

                // we need to setup neh.Start and End; these are instructions; we need to locate it in the source by index
                if (eh.TryStart != null)
                {
                    int idx = oldBody.Instructions.IndexOf(eh.TryStart);
                    neh.TryStart = col[idx];
                }
                if (eh.TryEnd != null)
                {
                    int idx = oldBody.Instructions.IndexOf(eh.TryEnd);
                    neh.TryEnd = col[idx];
                }

                nBody.ExceptionHandlers.Add(neh);
            }

            // Add this method to the target typedef
            copyToTypedef.Methods.Add(targetMethod);
            targetMethod.DeclaringType = copyToTypedef;
            return targetMethod;
        }

        public static void ReplaceMethod(MethodDefinition targetMethod, MethodDefinition sourceMethod)
        {
            ModuleDefinition targetModule = targetMethod.DeclaringType.Module;

            // Create second method with original code
            MethodDefinition realMethod = new MethodDefinition(targetMethod.Name+"_old", targetMethod.Attributes, targetMethod.ReturnType);
            {
                // Copy the parameters
                foreach (ParameterDefinition p in targetMethod.Parameters)
                {
                    ParameterDefinition nP = new ParameterDefinition(p.Name, p.Attributes, p.ParameterType);
                    realMethod.Parameters.Add(nP);
                }

                // Copy method contents
                MethodBody nBody = realMethod.Body;
                MethodBody oldBody = targetMethod.Body;
                nBody.InitLocals = oldBody.InitLocals;

                // copy the local variable definition
                foreach (VariableDefinition v in oldBody.Variables)
                {
                    VariableDefinition nv = new VariableDefinition(v.VariableType);
                    nBody.Variables.Add(nv);
                }

                // copy the IL
                Mono.Collections.Generic.Collection<Instruction> col = nBody.Instructions;
                foreach (Instruction i in oldBody.Instructions)
                {
                    col.Add(i);
                }

                // copy the exception handler blocks

                foreach (ExceptionHandler eh in oldBody.ExceptionHandlers)
                {
                    ExceptionHandler neh = new ExceptionHandler(eh.HandlerType);
                    neh.CatchType = eh.CatchType;

                    // we need to setup neh.Start and End; these are instructions; we need to locate it in the source by index
                    if (eh.TryStart != null)
                    {
                        int idx = oldBody.Instructions.IndexOf(eh.TryStart);
                        neh.TryStart = col[idx];
                    }
                    if (eh.TryEnd != null)
                    {
                        int idx = oldBody.Instructions.IndexOf(eh.TryEnd);
                        neh.TryEnd = col[idx];
                    }

                    nBody.ExceptionHandlers.Add(neh);
                }

                targetMethod.DeclaringType.Methods.Add(realMethod);
                realMethod.DeclaringType = targetMethod.DeclaringType;
            }

            // Replace original methods IL with proxy code
            {
                // copy the body
                MethodBody nBody = targetMethod.Body;
                MethodBody oldBody = sourceMethod.Body;

                nBody.InitLocals = oldBody.InitLocals;

                // copy the local variable definition
                nBody.Variables.Clear();
                foreach (VariableDefinition v in oldBody.Variables)
                {
                    VariableDefinition nv = new VariableDefinition(targetModule.ImportReference(v.VariableType));
                    nBody.Variables.Add(nv);
                }

                // copy the IL; we only need to take care of reference and method definitions
                var col = nBody.Instructions;
                col.Clear();
                foreach (Instruction i in oldBody.Instructions)
                {
                    object operand = i.Operand;
                    if (operand == null)
                    {
                        col.Add(Instruction.Create(i.OpCode));
                    }

                    // for any methoddef that this method calls, we will copy it

                    else if (operand is MethodDefinition)
                    {
                        MethodDefinition dmethod = operand as MethodDefinition;
                        MethodDefinition newMethod = CopyMethod(targetMethod.DeclaringType, dmethod); //TODO: maybe not
                        col.Add(Instruction.Create(i.OpCode, newMethod));
                    }

                    // for member reference, import it
                    else if (operand is FieldReference)
                    {
                        FieldReference fref = operand as FieldReference;
                        FieldReference newf = targetModule.ImportReference(fref);
                        col.Add(Instruction.Create(i.OpCode, newf));
                    }
                    else if (operand is TypeReference)
                    {
                        TypeReference tref = operand as TypeReference;
                        TypeReference newf = targetModule.ImportReference(tref);
                        col.Add(Instruction.Create(i.OpCode, newf));
                    }
                    else if (operand is TypeDefinition)
                    {
                        TypeDefinition tdef = operand as TypeDefinition;
                        TypeReference newf = targetModule.ImportReference(tdef);
                        col.Add(Instruction.Create(i.OpCode, newf));
                    }
                    else if (operand is MethodReference)
                    {
                        MethodReference mref = operand as MethodReference;
                        MethodReference newf = null;

                        /*MethodDefinition mdef = mref.Resolve();
                        if (mdef == sourceMethod)
                        {
                            newf = targetModule.ImportReference(targetMethod);
                        }
                        else */if (mref.FullName.Equals(targetMethod.FullName))
                        //else if (mdef == targetMethod)
                        {
                            newf = targetModule.ImportReference(realMethod);
                        }
                        else
                        {
                            newf = targetModule.ImportReference(mref);
                        }
                        col.Add(Instruction.Create(i.OpCode, newf));
                    }
                    else
                    {
                        // we don't need to do any processing on the operand
                        col.Add(i);
                    }
                }

                // copy the exception handler blocks
                nBody.ExceptionHandlers.Clear();
                foreach (ExceptionHandler eh in oldBody.ExceptionHandlers)
                {
                    ExceptionHandler neh = new ExceptionHandler(eh.HandlerType);
                    neh.CatchType = targetModule.ImportReference(eh.CatchType);

                    // we need to setup neh.Start and End; these are instructions; we need to locate it in the source by index
                    if (eh.TryStart != null)
                    {
                        int idx = oldBody.Instructions.IndexOf(eh.TryStart);
                        neh.TryStart = col[idx];
                    }
                    if (eh.TryEnd != null)
                    {
                        int idx = oldBody.Instructions.IndexOf(eh.TryEnd);
                        neh.TryEnd = col[idx];
                    }

                    nBody.ExceptionHandlers.Add(neh);
                }
            }
        }
    }
}
