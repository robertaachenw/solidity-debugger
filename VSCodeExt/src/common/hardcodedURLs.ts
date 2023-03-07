import {NodeArchType, NodePlatformType} from "./common";

export const PORTABLE_DIR = 'portable';


export interface IReleaseJson {
    latestRelease: string
    dotNetInstallerSha256?: string
    engineInstallerSha256?: string
    solidityInstallerSha256?: string
}

export interface IPlatformUrl {
    platform: NodePlatformType
    arch: NodeArchType
    urls: string[]
}

export const DOCS_URL = 'https://soliditydebugger.org/docs/';
export const TUTORIAL_URL = 'https://soliditydebugger.org/docs/basicTutorial.html';
export const HOMEPAGE_URL = 'https://releases.soliditydebugger.org';
export const HOMEPAGE_JSON_URL = `${HOMEPAGE_URL}/latest/release.json`;

export const GITHUB_URL = 'https://github.com/robertaachenw/solidity-debugger';

export const GITHUB_PROJECT_TEMPLATE_EMPTY_URL = `${GITHUB_URL}/releases/download/{0}/example-empty.zip`;
export const GITHUB_PROJECT_TEMPLATE_ERC20_URL = `${GITHUB_URL}/releases/download/{0}/example-erc20.zip`;
export const GITHUB_PROJECT_TEMPLATE_HARDHAT_URL = `${GITHUB_URL}/releases/download/{0}/example-hardhat.zip`;

export const GITHUB_RELEASE_URL = `${GITHUB_URL}/releases/download/{0}`;
export const GITHUB_WEBINST_ENGINE_URL = `${GITHUB_RELEASE_URL}/engine.js`;
export const GITHUB_WEBINST_SOLIDITY_URL = `${GITHUB_RELEASE_URL}/solidity.js`;

export let ENGINE_URLS: IPlatformUrl[] = [
    {platform: 'win32', arch: 'ia32', urls: [`${GITHUB_RELEASE_URL}/sdbg-engine-win-x86.zip`,]},
    {platform: 'win32', arch: 'x64', urls: [`${GITHUB_RELEASE_URL}/sdbg-engine-win-x64.zip`,]},
    {platform: 'win32', arch: 'arm', urls: [`${GITHUB_RELEASE_URL}/sdbg-engine-win-arm.zip`,]},
    {platform: 'win32', arch: 'arm64', urls: [`${GITHUB_RELEASE_URL}/sdbg-engine-win-arm64.zip`,]},

    {platform: 'linux', arch: 'arm', urls: [`${GITHUB_RELEASE_URL}/sdbg-engine-linux-arm.zip`,]},
    {platform: 'linux', arch: 'arm64', urls: [`${GITHUB_RELEASE_URL}/sdbg-engine-linux-arm64.zip`,]},
    {platform: 'linux', arch: 'x64', urls: [`${GITHUB_RELEASE_URL}/sdbg-engine-linux-x64.zip`,]},

    {platform: 'darwin', arch: 'x64', urls: [`${GITHUB_RELEASE_URL}/sdbg-engine-osx-x64.zip`,]},
    {platform: 'darwin', arch: 'arm64', urls: [`${GITHUB_RELEASE_URL}/sdbg-engine-osx-arm64.zip`,]},
];

export const SOLC_REPO_URL = 'https://binaries.soliditylang.org/bin';
export const SOLC_LIST_JSON_URL = `${SOLC_REPO_URL}/list.json`;
export const SOLC_JS_URL = `${SOLC_REPO_URL}/{0}`;
