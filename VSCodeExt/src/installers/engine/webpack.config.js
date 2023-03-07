const path = require('path');
const forkTsCheckerWebpackPlugin = require('fork-ts-checker-webpack-plugin');

module.exports = {
    optimization: {
        minimize: true
    },

    devtool: 'source-map',

    mode: 'production',

    entry: './src/engineInstaller.ts',

    output: {
        path: path.resolve(__dirname, '../../../portable'),
        filename: 'engine.js',
        libraryTarget: 'umd',
        library: 'MyLib',
        umdNamedDefine: true
    },

    resolve: {
        extensions: ['.ts', '.js'],
    },

    target: 'node',

    module: {
        rules: [{
            test: /\.tsx?/, use: {
                loader: 'ts-loader', options: {
                    transpileOnly: true,
                }
            }, exclude: /node_modules/,
        }]
    },

    plugins: [new forkTsCheckerWebpackPlugin(),],

    watch: true
};
