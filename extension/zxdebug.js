var vscode = require('vscode');

function activate(context) {

    context.subscriptions.push(
        vscode.commands.registerCommand('zxdebug.testCommand', function() {
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