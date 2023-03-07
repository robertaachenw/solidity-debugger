using Meadow.EVM.Data_Types;
using Meadow.EVM.EVM.Execution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace Meadow.EVM.EVM.Instructions.Block_Information
{
    public class InstructionBlockTimestamp : InstructionBase
    {
        /// <summary>
        /// Our default constructor, reads the opcode/operand information from the provided stream.
        /// </summary>
        public InstructionBlockTimestamp(MeadowEVM evm) : base(evm) { }

        public override void Execute()
        {
            if (EVM.State.SpoofBlockTimestamp != BigInteger.Zero)
            {
                Stack.Push(EVM.State.SpoofBlockTimestamp);
            }
            else
            {
                Stack.Push(EVM.State.CurrentBlock.Header.Timestamp);
            }
        }
    }

}
