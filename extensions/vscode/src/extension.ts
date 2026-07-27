import * as fs from "fs";
import * as path from "path";
import * as vscode from "vscode";
import {
  ExecuteCommandRequest,
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";

let client: LanguageClient | undefined;
let configWatcher: vscode.FileSystemWatcher | undefined;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  context.subscriptions.push(
    vscode.commands.registerCommand("sqlguardian.analyze", async () => {
      const editor = vscode.window.activeTextEditor;
      if (!editor || editor.document.languageId !== "sql") {
        vscode.window.showInformationMessage("Open a .sql file to analyze with SQLGuardian.");
        return;
      }

      if (!client) {
        vscode.window.showErrorMessage("SQLGuardian language server is not running.");
        return;
      }

      await editor.document.save();
      const configPath = vscode.workspace
        .getConfiguration("sqlguardian")
        .get<string>("configPath")
        ?.trim();

      await client.sendRequest(ExecuteCommandRequest.type, {
        command: "sqlguardian.analyze",
        arguments: [configPath || null, editor.document.uri.toString()],
      });
    }),
    vscode.commands.registerCommand("sqlguardian.restartServer", async () => {
      await restart(context);
      vscode.window.showInformationMessage("SQLGuardian language server restarted.");
    }),
    {
      dispose: () => {
        void stopClient();
      },
    }
  );

  try {
    await start(context);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    vscode.window.showErrorMessage(`SQLGuardian failed to start: ${message}`);
  }
}

export async function deactivate(): Promise<void> {
  await stopClient();
}

async function restart(context: vscode.ExtensionContext): Promise<void> {
  await stopClient();
  await start(context);
}

async function stopClient(): Promise<void> {
  configWatcher?.dispose();
  configWatcher = undefined;

  const current = client;
  client = undefined;
  if (!current) {
    return;
  }

  try {
    await current.stop();
  } catch {
    // Host may already be tearing down (Cursor DisposableStore noise).
  }
}

async function start(context: vscode.ExtensionContext): Promise<void> {
  const config = vscode.workspace.getConfiguration("sqlguardian");
  const dotnetPath = config.get<string>("dotnet.path") || "dotnet";
  const serverDll = resolveServerDll(context.extensionPath, config.get<string>("server.path") || "");

  if (!serverDll) {
    vscode.window.showErrorMessage(
      "SQLGuardian language server DLL not found. Run scripts\\publish-vscode-server.cmd or build the LanguageServer project."
    );
    return;
  }

  const serverOptions: ServerOptions = {
    run: {
      command: dotnetPath,
      args: [serverDll],
      transport: TransportKind.stdio,
    },
    debug: {
      command: dotnetPath,
      args: [serverDll],
      transport: TransportKind.stdio,
    },
  };

  configWatcher = vscode.workspace.createFileSystemWatcher("**/sqlguardian.json");

  const clientOptions: LanguageClientOptions = {
    documentSelector: [
      { scheme: "file", language: "sql" },
      { scheme: "untitled", language: "sql" },
    ],
    synchronize: {
      fileEvents: configWatcher,
    },
    outputChannelName: "SQLGuardian",
  };

  client = new LanguageClient(
    "sqlguardian",
    "SQLGuardian",
    serverOptions,
    clientOptions
  );

  // Own lifecycle explicitly — avoid double-dispose via context.subscriptions.
  await client.start();
}

function resolveServerDll(extensionPath: string, configuredPath: string): string | undefined {
  const candidates: string[] = [];

  if (configuredPath.trim()) {
    candidates.push(configuredPath.trim());
  }

  candidates.push(path.join(extensionPath, "server", "SQLGuardian.LanguageServer.dll"));

  const repoRoot = path.resolve(extensionPath, "..", "..");
  candidates.push(
    path.join(
      repoRoot,
      "src",
      "SQLGuardian.LanguageServer",
      "bin",
      "Release",
      "net9.0",
      "SQLGuardian.LanguageServer.dll"
    )
  );
  candidates.push(
    path.join(
      repoRoot,
      "src",
      "SQLGuardian.LanguageServer",
      "bin",
      "Debug",
      "net9.0",
      "SQLGuardian.LanguageServer.dll"
    )
  );

  return candidates.find((p) => fs.existsSync(p));
}
