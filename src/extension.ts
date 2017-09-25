'use strict';

import * as vscode from 'vscode';

export function activate(context: vscode.ExtensionContext) {

	context.subscriptions.push(
		vscode.commands.registerCommand('extension.zxdebug.startSession',
			config => startSession(config)
		)	
	);
}

function startSession(config) {

    // provide all the workspace config items to the adapter by
    // adding them to the launch/attach config it already gets
    config.workspaceConfiguration = vscode.workspace.getConfiguration('zxdebug');

    vscode.commands.executeCommand('vscode.startDebug', config);

    // lxanguages.registerHoverProvider('zx-debug-disasm', {
    //     provideHover(document, position, token) {
    //         return new Hover('I am a hover!');
    //     }
    // });

    return { status: 'ok' };
}
