using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Meadow.Core.EthTypes;
using Meadow.EVM.EVM.Definitions;
using Meadow.EVM.EVM.Execution;

namespace Meadow.EVM.EVM.Instructions.Bitwise_Logic
{
    public class InstructionSar : InstructionBase
    {
        //#region Constructors
        /// <summary>
        /// Our default constructor, reads the opcode/operand information from the provided stream.
        /// </summary>
        public InstructionSar(MeadowEVM evm) : base(evm) { }
        //#endregion

        //#region Functions
        public override void Execute()
        {
            var shift = Stack.Pop();
            var signedValue = Stack.Pop(true);

            BigInteger result = 0;
            if (shift < 256)
            {
                result = signedValue >> (int)shift;
            }
            else if (signedValue >= 0)
            {
                result = 0;
            }
            else
            {
                result = UInt256.MaxValueAsBigInt;
            }

            Stack.Push(result);
        }
        //#endregion
    }
}
