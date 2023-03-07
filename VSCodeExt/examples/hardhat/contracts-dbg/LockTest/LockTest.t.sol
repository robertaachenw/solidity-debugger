// SPDX-License-Identifier: MIT
pragma solidity >= 0.4.21 < 0.9.0;

interface ILock {
    function owner() external view returns (address);
    function unlockTime() external view returns (uint);
    function withdraw() external;
}

contract DbgEntry {
    event EvmSetBlockTimestamp(uint256);
    event EvmSpoofMsgSender(address);
    event EvmUnspoof();

    event EvmPrint(string);
    event EvmPrint(address);
    event EvmPrint(uint256);

    constructor() {
        ILock lock = ILock(0x5FbDB2315678afecb367f032d93F642f64180aa3);

        address lockOwner = lock.owner();
        uint256 lockUnlockTime = lock.unlockTime();

        // spoof block.timestamp
        emit EvmSetBlockTimestamp(lockUnlockTime + 1);

        // spoof msg.sender as seen by lock.withdraw()
        emit EvmSpoofMsgSender(lockOwner);

        // test lock.withdraw()
        uint256 balanceBefore = lockOwner.balance;
        lock.withdraw();
        uint256 fundsWithdrawn = lockOwner.balance - balanceBefore;

        // cleanup
        emit EvmUnspoof();
    }
}