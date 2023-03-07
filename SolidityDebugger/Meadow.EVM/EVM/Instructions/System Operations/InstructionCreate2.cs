using Meadow.EVM.Data_Types.Addressing;
using Meadow.EVM.EVM.Definitions;
using Meadow.EVM.EVM.Messages;
using Meadow.EVM.EVM.Execution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Meadow.EVM.Configuration;
using Meadow.EVM.Data_Types;
using Meadow.EVM.Exceptions;
using Meadow.Core.Utils;

namespace Meadow.EVM.EVM.Instructions.System_Operations
{
    public class InstructionCreate2 : InstructionBase
    {
        //#region Constructors
        /// <summary>
        /// Our default constructor, reads the opcode/operand information from the provided stream.
        /// </summary>
        public InstructionCreate2(MeadowEVM evm) : base(evm) { }
        //#endregion

        //#region Functions
        public override void Execute()
        {
            // Obtain the values for our call value, and call data memory.
            var paramValue = Stack.Pop();
            var paramOffset = Stack.Pop();
            var paramSize = Stack.Pop();
            var paramSalt = Stack.Pop();

            // We'll want to charge for memory expansion first
            Memory.ExpandStream(paramOffset, paramSize);

            // If we're in a static context, we can't self destruct
            if (Message.IsStatic)
            {
                throw new EVMException($"{Opcode.ToString()} instruction cannot execute in a static context!");
            }

            // Verify we have enough balance and call depth hasn't exceeded the maximum.
            if (EVM.State.GetBalance(Message.To) >= paramValue && Message.Depth < EVMDefinitions.MAX_CALL_DEPTH)
            {
                // Obtain our call information.
                byte[] callData = Memory.ReadBytes((long)paramOffset, (int)paramSize);
                BigInteger innerCallGas = GasState.Gas;
                if (Version >= EthereumRelease.TangerineWhistle)
                {
                    innerCallGas = GasDefinitions.GetMaxCallGas(innerCallGas);
                }

                // Create our message
                var message = new EVMMessage(
                    Message.To, 
                    Address.ZERO_ADDRESS, 
                    paramValue, 
                    innerCallGas, 
                    callData, 
                    Message.Depth + 1, 
                    Address.ZERO_ADDRESS, 
                    true, 
                    Message.IsStatic);
                
                var innerVmResult = MeadowEVM.CreateContract2(EVM.State, message, paramSalt);
                if (innerVmResult.Succeeded)
                {
                    // Push our resulting address onto the stack.
                    Stack.Push(BigIntegerConverter.GetBigInteger(innerVmResult.ReturnData.ToArray()));
                    EVM.ExecutionState.LastCallResult = null;
                }
                else
                {
                    // We failed, push our fail value and put the last call data in place.
                    Stack.Push(0);
                    ExecutionState.LastCallResult = innerVmResult;
                }
            }
            else
            {
                // We didn't have a sufficient balance or call depth so we push nothing to the stack. We push 0 (fail)
                Stack.Push(0);

                // Set our last call result as null.
                ExecutionState.LastCallResult = null;
            }
        }
        //#endregion
    }
}
