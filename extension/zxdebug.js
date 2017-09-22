var vscode = require('vscode');

function activate(context) {

    context.subscriptions.push(
        vscode.commands.registerCommand('extension.zxdebug.startSession',
            config => startSession(config)
        )
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('extension.zxdebug.testCommand', function() {
            //vscode.debug.activeDebugSession.customRequest("hello");
            try
            {
                vscode.workspace.openTextDocument("D:\\Dev\\ZX\\test1\\.zxdbg\\disasm.zdis").then(doc => {
                    vscode.window.showTextDocument(doc);                   
                });
            }
            catch( e )
            {
                console.log( e );
            }
        })
    );

    vscode.debug.onDidReceiveDebugSessionCustomEvent(e => {
        if( e.event == 'refreshDisasm' ) {
            vscode.workspace.openTextDocument( e.body.file ).then(doc => {
                vscode.window.showTextDocument(doc);                   
            });
            // console.log( "refreshDisasm" );
        }
    });
}
exports.activate = activate;

function deactivate() {
}
exports.deactivate = deactivate;


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
