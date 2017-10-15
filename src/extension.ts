'use strict';

import * as vscode from 'vscode';

import ConfigurationProvider from './config';
import HoverProvider from './hoverProvider'
import DefinitionProvider from './definitionProvider'

export function activate( context: vscode.ExtensionContext ) 
{
    context.subscriptions.push(
        
        vscode.debug.registerDebugConfigurationProvider(
            'zxdebug',
            new ConfigurationProvider()
        ),

        vscode.debug.onDidTerminateDebugSession( e => {
                cleanup();
            }
        ),

        vscode.debug.onDidReceiveDebugSessionCustomEvent(e => {
            handleDebugEvent( e.event, e.body );
        }),

		vscode.commands.registerCommand('extension.zxdebug.setNextStatement',
            args => setNextStatement(args)
        ),

        // vscode.commands.registerCommand('extension.zxdebug.inspectMemory',
        //     args => inspectMemory(args)
        // ),

        vscode.commands.registerCommand('extension.zxdebug.watchMemory',
            args => watchMemory(args)
        ),

        vscode.languages.registerHoverProvider(
            '*',
            new HoverProvider()
        ),

        vscode.languages.registerDefinitionProvider(
            '*',
            new DefinitionProvider()
        )
    )
}

function cleanup()
{
    Object.keys(memoryWatches).forEach(key => {
        let value = memoryWatches[key];
        value.hide();
        value.dispose();
      });
}

function handleDebugEvent( event, body )
{
    switch( event )
    {
        case 'setDisassemblyLine':
            setDisassemblyLine( body );
            break;

        case 'memoryWatchUpdated':
            memoryWatchUpdated( body.id, body.data );
            break;
    } 
}

let memoryWatches: { [key: string]: vscode.OutputChannel } = {};
function watchMemory( args ) 
{
    vscode.window.showInputBox({prompt: "Address?"})
        .then( address => {
            
            if( address == undefined )
                return;

            vscode.window.showInputBox({prompt: "Length?"})
                .then( length => {

                    if( vscode.debug.activeDebugSession === undefined )
                        return;

                    var id = "OC" + (Object.keys(memoryWatches).length+1);
                    var channel = vscode.window.createOutputChannel("Watch " + address);
                    channel.show();

                    memoryWatches[id] = channel;

                    vscode.debug.activeDebugSession.customRequest(
                        "watchMemory",
                        {
                            id: id,
                            address: address,
                            length: length || ''
                        }
                    );

                })
        }
    )
}
function memoryWatchUpdated( id, data )
{
    var ch = memoryWatches[id];
    ch.appendLine( data );
}

function setNextStatement( args ) : Thenable<any> {
    
    if( vscode.debug.activeDebugSession === undefined 
     || vscode.window.activeTextEditor === undefined
     || args === undefined )
        return Promise.resolve(null);

    return vscode.debug.activeDebugSession.customRequest(
        "setNextStatement",
        { 
            file: args.fsPath, 
            line: vscode.window.activeTextEditor.selection.start.line
        }
    ).then( reply => {
        // ok
    }, err => {
        throw err;
    });
}

var decorators : vscode.TextEditorDecorationType[] = [];
function setDisassemblyLine( args ) {

    decorators.forEach(decorator => {
        decorator.dispose();
    });

    vscode.window.visibleTextEditors.forEach( editor => {
        
        var document = editor.document;
        
        if( document.fileName.endsWith('disasm.zdis') )
        {
            var decorator = vscode.window.createTextEditorDecorationType(
            {
                isWholeLine: true,
                backgroundColor: new vscode.ThemeColor('debugExceptionWidget.background')
            });
                
            var decRange = new vscode.Range(args.line,0,args.line,0);
            var revealRange = new vscode.Range(args.line-3,0,args.line+3,0);
            
            editor.setDecorations(
                decorator,
                [decRange]
            );

            editor.revealRange( revealRange );
            editor.selection = new vscode.Selection(args.line,0, args.line, 99999);

            decorators.push( decorator );
        }

    });
}

