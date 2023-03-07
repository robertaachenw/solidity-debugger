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
    public class InstructionShl : InstructionBase
    {
        //#region Constructors
        /// <summary>
        /// Our default constructor, reads the opcode/operand information from the provided stream.
        /// </summary>
        public InstructionShl(MeadowEVM evm) : base(evm) { }
        //#endregion

        //#region Functions
        public override void Execute()
        {
            var shift = Stack.Pop();
            var value = Stack.Pop();

            BigInteger result = 0;
            
            if (shift < 256)
            {
                result = (value << (int)shift) % (UInt256.MaxValueAsBigInt + 1);
            }

            Stack.Push(result);
        }
        //#endregion
    }
}
