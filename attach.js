var vscode = require('vscode');

function activate(context) {

    var disposable = vscode.commands.registerCommand('attach.attachToDebugger', function () {

        var config = {
            name:string,
            type:"zesarux",
            request:"launch"
        };

        var call = vscode.commands.executeCommand("vscode.startDebug", config);
        call.then(function(response){
            console.log(response);
        },function(error)
        {
            console.log(error);
        });

    });

    context.subscriptions.push(disposable);
}
exports.activate = activate;

function deactivate() {
}
exports.deactivate = deactivate;