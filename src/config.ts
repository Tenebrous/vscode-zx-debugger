'use strict';

import * as vscode from 'vscode';

export default class ConfigurationProvider implements vscode.DebugConfigurationProvider {
    
    /**
     * returns initial debug configurations.
     */
    provideDebugConfigurations(
        folder: vscode.WorkspaceFolder | undefined, 
        token?: vscode.CancellationToken): vscode.ProviderResult<vscode.DebugConfiguration[]> {

    const config = {
        "type": "zxdebug",
        "name": "ZX Debugger",
        "request": "attach",
        "projectFolder": "${workspaceRoot}",
        "sourceMaps": [
            "48k_rom.dbg"
        ],
        "opcodeTables": [
            "z80.tbl"
        ],
        "stopOnEntry": true
    }
    
    return [ config ];
    }
    
    /**
     * "massage" launch configuration before starting the session.
     */
    resolveDebugConfiguration(
        folder: vscode.WorkspaceFolder | undefined, 
        config: vscode.DebugConfiguration, 
        token?: vscode.CancellationToken): vscode.ProviderResult<vscode.DebugConfiguration> {

    config.workspaceConfiguration = vscode.workspace.getConfiguration('zxdebug');
    
    return config;
    }
}