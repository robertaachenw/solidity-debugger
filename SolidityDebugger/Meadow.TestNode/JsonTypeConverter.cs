﻿using Meadow.Core.EthTypes;
using Meadow.EVM.Data_Types;
using Meadow.EVM.EVM.Definitions;
using Meadow.JsonRpc.Types;
using Meadow.JsonRpc.Types.Debugging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Meadow.Core.Utils;

namespace Meadow.TestNode
{
    internal abstract class JsonTypeConverter
    {
        internal static ExecutionTrace CoreExecutionTraceJsonExecutionTrace(Meadow.EVM.Debugging.Tracing.ExecutionTrace coreExecutionTrace)
        {
            // If the execution trace is null, we return null
            if (coreExecutionTrace == null)
            {
                return null;
            }
            
            // Create our array of trace points
            ExecutionTracePoint[] tracepoints = new ExecutionTracePoint[coreExecutionTrace.Tracepoints.Count];

            // Populate all tracepoint items.
            for (int i = 0; i < tracepoints.Length; i++)
            {
                // Obtain our trace point.
                var coreTracePoint = coreExecutionTrace.Tracepoints[i];
                
                // Define our memory and stack
                Data[] executionMemory = null;
                Data[] executionStack = coreTracePoint.Stack.Select(s => new Data(s)).ToArray();
  
                if (coreTracePoint.Memory != null)
                {
                    int wordCount = (int)EVMDefinitions.GetWordCount(coreTracePoint.Memory.Length);
                    Span<byte> memorySpan = coreTracePoint.Memory; 
                    executionMemory = new Data[wordCount];
                    for (int x = 0; x < executionMemory.Length; x++)
                    {
                        executionMemory[x] = new Data(memorySpan.Slice(x * EVMDefinitions.WORD_SIZE, EVMDefinitions.WORD_SIZE));
                    }
                }

                // Obtain our memory
                tracepoints[i] = new ExecutionTracePoint()
                {
                    CallData = coreTracePoint.CallData,
                    Code = coreTracePoint.Code,
                    ContractAddress = coreTracePoint.ContractAddress == null ? (Address?)null : new Address(coreTracePoint.ContractAddress.ToByteArray()),
                    ContractDeployed = coreTracePoint.ContractDeployed,
                    Opcode = coreTracePoint.Opcode,
                    GasRemaining = (UInt256)coreTracePoint.GasRemaining,
                    GasCost = (UInt256)coreTracePoint.GasCost,
                    PC = coreTracePoint.PC,
                    Depth = coreTracePoint.Depth,
                    Stack = executionStack,
                    Memory = executionMemory,
                    Storage = coreTracePoint.Storage,
                    Emit = null
                };
                
                if (coreTracePoint.Emit != null)
                {
                    var emitData = new ExecutionTracePointEmit();
                    emitData.Address = new Address(coreTracePoint.Emit.Address.ToByteArray());
                    emitData.Data = coreTracePoint.Emit.Data;
                    emitData.Topics = coreTracePoint.Emit.Topics.Select(s => new UInt256(s).ToHexString()).ToArray();
                    
                    tracepoints[i].Emit = emitData;
                }
            }

            // Create our array of exceptions
            var exceptions = new ExecutionTraceException[coreExecutionTrace.Exceptions.Count];

            // Populate all exception items
            for (var i = 0; i < exceptions.Length; i++)
            {
                // Set our item.
                exceptions[i] = new ExecutionTraceException()
                {
                    TraceIndex = coreExecutionTrace.Exceptions[i].TraceIndex,
                    Message = coreExecutionTrace.Exceptions[i].Exception.Message
                };
            }

            // Obtain our execution trace analysis.
            var executionTrace = new ExecutionTrace()
            {
                Tracepoints = tracepoints,
                Exceptions = exceptions
            };
            
            // Return our execution trace.
            return executionTrace;
        }

        internal static CompoundCoverageMap CoreCompoundCoverageMapsToJsonCompoundCoverageMaps((Meadow.EVM.Debugging.Coverage.CodeCoverage.CoverageMap undeployedMap, Meadow.EVM.Debugging.Coverage.CodeCoverage.CoverageMap deployedMap) compoundMaps)
        {
            // Return our compounded coverage map.
            return new CompoundCoverageMap()
            {
                UndeployedMap = CoreCoverageMapToJsonCoverageMap(compoundMaps.undeployedMap),
                DeployedMap = CoreCoverageMapToJsonCoverageMap(compoundMaps.deployedMap)
            };
        }

        internal static CoverageMap CoreCoverageMapToJsonCoverageMap(Meadow.EVM.Debugging.Coverage.CodeCoverage.CoverageMap coverageMap)
        {
            // If the coverage map is null, we return null
            if (coverageMap == null)
            {
                return null;
            }

            // Create our json coverage map object.
            CoverageMap jsonCoverageMap = new CoverageMap
            {
                ContractAddress = new Address(coverageMap.ContractAddress.ToByteArray()),
                Map = coverageMap.Map.ToArray()
            };

            // Copy our jump offsets.
            jsonCoverageMap.JumpOffsets = new int[coverageMap.JumpOffsets.Count];
            coverageMap.JumpOffsets.CopyTo(jsonCoverageMap.JumpOffsets);

            // Copy our non-jump offsets.
            jsonCoverageMap.NonJumpOffsets = new int[coverageMap.NonJumpOffsets.Count];
            coverageMap.NonJumpOffsets.CopyTo(jsonCoverageMap.NonJumpOffsets);

            // Copy our code over
            jsonCoverageMap.Code = coverageMap.Code.ToArray();

            // Return our json coverage map
            return jsonCoverageMap;
        }

        internal static Block CoreBlockToJsonBlock(TestNodeChain testChain, Meadow.EVM.Data_Types.Block.Block block)
        {
            // Initialize our block
            Block jsonBlock = new Block();

            // Set all of our properties.
            jsonBlock.Difficulty = (ulong)block.Header.Difficulty;
            jsonBlock.ExtraData = block.Header.ExtraData;
            jsonBlock.GasLimit = (ulong)block.Header.GasLimit;
            jsonBlock.GasUsed = (ulong)block.Header.GasUsed;
            jsonBlock.Hash = new Hash(block.Header.GetHash());
            jsonBlock.LogsBloom = BigIntegerConverter.GetBytes(block.Header.Bloom, Meadow.EVM.EVM.Definitions.EVMDefinitions.BLOOM_FILTER_SIZE);
            jsonBlock.Miner = new Address(block.Header.Coinbase.ToByteArray());
            jsonBlock.MixHash = new Hash(block.Header.MixHash);
            jsonBlock.Nonce = (ulong)BigIntegerConverter.GetBigInteger(block.Header.Nonce, false, sizeof(ulong));
            jsonBlock.Number = (ulong)block.Header.BlockNumber;
            jsonBlock.ParentHash = new Hash(block.Header.PreviousHash);
            jsonBlock.ReceiptsRoot = new Data(block.Header.ReceiptsRootHash);
            jsonBlock.Sha3Uncles = new Hash(block.Header.UnclesHash);
            jsonBlock.Size = 0; // TODO:
            jsonBlock.StateRoot = new Data(block.Header.StateRootHash);
            jsonBlock.Timestamp = (ulong)block.Header.Timestamp;
            jsonBlock.TotalDifficulty = (ulong)testChain.Chain.GetScore(block); // TODO: Referring to blocks of different times won't work with this since chain itself won't revert properly, verify.
            jsonBlock.Transactions = null; // TODO: Set this
            jsonBlock.TransactionsRoot = new Data(block.Header.TransactionsRootHash);
            jsonBlock.Uncles = new Hash?[block.Uncles.Length];

            // Set all of our uncles as the hashes of the uncle headers.
            for (int i = 0; i < block.Uncles.Length; i++)
            {
                jsonBlock.Uncles[i] = new Hash(block.Uncles[i].GetHash());
            }

            // Return the json representation of the block.
            return jsonBlock;
        }

        internal static TransactionObject CoreTransactionToJsonTransaction(Meadow.EVM.Data_Types.Block.Block block, ulong transactionIndex)
        {
            // Obtain the transaction
            Meadow.EVM.Data_Types.Transactions.Transaction transaction = block.Transactions[(int)transactionIndex];

            // Obtain our block properties
            Hash blockHash = new Hash(block.Header.GetHash());
            ulong blockNumber = (ulong)block.Header.BlockNumber;

            // Initialize our transaction
            TransactionObject jsonTransaction = new TransactionObject()
            {
                BlockHash = blockHash,
                BlockNumber = blockNumber,
                From = new Address(transaction.GetSenderAddress().ToByteArray()),
                Gas = (ulong)transaction.StartGas,
                GasPrice = (ulong)transaction.GasPrice,
                Hash = new Hash(transaction.GetHash()),
                Input = transaction.Data,
                Nonce = (ulong)transaction.Nonce,
                To = new Address(transaction.To.ToByteArray()),
                Value = (UInt256)transaction.Value
            };

            // Return our json representation of our string.
            return jsonTransaction;
        }

        internal static FilterLogObject CoreLogToJsonLog(Meadow.EVM.Data_Types.Transactions.Log log, int logIndex, ulong? blockNumber, ulong? transactionIndex, Hash transactionHash, Hash blockHash)
        {
            // Initialize a new log
            FilterLogObject jsonLog = new FilterLogObject()
            {
                Address = new Address(log.Address.ToByteArray()),
                LogIndex = (ulong)logIndex,
                BlockNumber = blockNumber,
                TransactionIndex = transactionIndex,
                TransactionHash = transactionHash,
                BlockHash = blockHash,
                Removed = false,
            };

            // Set all of our topics
            jsonLog.Topics = new Data[log.Topics.Count];
            for (int x = 0; x < jsonLog.Topics.Length; x++)
            {
                jsonLog.Topics[x] = new Data(BigIntegerConverter.GetBytes(log.Topics[x], EVMDefinitions.WORD_SIZE));
            }

            // Set our log data
            jsonLog.Data = log.Data;

            // Return our json log.
            return jsonLog;
        }

    }
}
