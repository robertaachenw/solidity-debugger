import { HardhatUserConfig } from "hardhat/config";
import "@nomicfoundation/hardhat-toolbox";

const config: HardhatUserConfig = {
  solidity: "0.8.17",
  networks: {
    local_23074: {
      url: "http://127.0.0.1:23074"
    }
  }
};

export default config;
