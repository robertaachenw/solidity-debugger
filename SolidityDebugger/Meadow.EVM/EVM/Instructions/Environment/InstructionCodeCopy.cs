﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Meadow.EVM.Configuration;
using Meadow.EVM.Data_Types;
using Meadow.EVM.EVM.Definitions;
using Meadow.EVM.EVM.Execution;

namespace Meadow.EVM.EVM.Instructions.Environment
{
    public class InstructionCodeCopy : InstructionBase
    {
        //#region Constructors
        /// <summary>
        /// Our default constructor, reads the opcode/operand information from the provided stream.
        /// </summary>
        public InstructionCodeCopy(MeadowEVM evm) : base(evm) { }
        //#endregion

        //#region Functions
        public override void Execute()
        {
            // Obtain our offsets and sizes for the copy.
            BigInteger memoryOffset = Stack.Pop();
            BigInteger dataOffset = Stack.Pop();
            BigInteger dataSize = Stack.Pop();

            // This is considered a copy operation, so we charge for the size of the data.
            GasState.Deduct(GasDefinitions.GetMemoryCopyCost(Version, dataSize));

            // Read our data from the code segment (if our offset is wrong or we hit the end of the array, the rest should be zeroes).
            byte[] data = new byte[(int)dataSize];
            int length = data.Length;
            if (dataOffset > EVM.Code.Length)
            {
                dataOffset = 0;
                length = 0;
            }
            else if (dataOffset + length > EVM.Code.Length)
            {
                length = EVM.Code.Length - (int)dataOffset;
            }

            // Copy to the memory location.
            EVM.Code.Slice((int)dataOffset, length).CopyTo(data);

            // Write the data to our given memory offset.
            Memory.Write((long)memoryOffset, data);
        }
        //#endregion
    }
}
