// SPDX-License-Identifier: MIT
import "./MyToken.sol";

contract DbgEntry {
    constructor() {
        MyToken myToken = new MyToken("tokenName", "tokenSymbol");
        string memory name = myToken.name();
        uint256 totalSupply = myToken.totalSupply();
        uint256 myBalance = myToken.balanceOf(address(this));
        uint256 done = 1;
    }
}
