'use strict';

import * as vscode from 'vscode';

export default class HoverProvider implements vscode.HoverProvider {
    provideHover(
        document: vscode.TextDocument, 
        position: vscode.Position, 
        token: vscode.CancellationToken): vscode.ProviderResult<vscode.Hover> {
        
        if( vscode.debug.activeDebugSession === undefined )
            return undefined;
       
        let wordRange = document.getWordRangeAtPosition(position);
        let lineRange = new vscode.Range( position.line, 0, position.line, 9999 );
        
        return vscode.debug.activeDebugSession.customRequest(
            "getHover",
            { 
                file:   document.fileName, 
                line:   position.line,
                column: position.character,
                text:   document.getText( lineRange ),
                symbol: document.getText( wordRange ),
            }
        ).then( reply => {
            return new vscode.Hover(
                new vscode.MarkdownString( reply.result )
            );
        }, err => {
            throw err;
        });

    }
}