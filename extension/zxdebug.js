var vscode = require('vscode');

function activate(context) {

    context.subscriptions.push(
        vscode.commands.registerCommand('zxdebug.testCommand', function() {
            vscode.debug.activeDebugSession.customRequest("hello");
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('zxdebug.attachToDebugger', function () {

            var config = {
                name:string,
                type:"zesarux",
                request:"attach"
            };

            var call = vscode.commands.executeCommand("vscode.startDebug", config);
            call.then(
                function(response) {
                    console.log(response);
                }, function(error) {
                    console.log(error);
                }
            );
            
        })
    );
}
exports.activate = activate;

function deactivate() {
}
exports.deactivate = deactivate;