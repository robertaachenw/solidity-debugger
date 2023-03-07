// SPDX-License-Identifier: MIT
pragma solidity >= 0.4.21 < 0.9.0;

contract DbgEntry {
    event EvmPrint(string);

    /**
     * @dev Returns the largest of two signed numbers.
     */
    function max(int256 a, int256 b) internal pure returns (int256) {
        return a >= b ? a : b;
    }

    /**
     * @dev Returns the average of two signed numbers without overflow.
     * The result is rounded towards zero.
     */
    function average(int256 a, int256 b) internal pure returns (int256) {
        // Formula from the book "Hacker's Delight"
        int256 x = (a & b) + ((a ^ b) >> 1);
        return x + (int256(uint256(x) >> 255) & (a ^ b));
    }

    /**
     * @dev Sdbg entry point
     */
    constructor() {
        emit EvmPrint("enter");

        int256 maxResult = max(1, 2);

        int256 averageResult = average(4, 8);

        emit EvmPrint("leave");
    }
}