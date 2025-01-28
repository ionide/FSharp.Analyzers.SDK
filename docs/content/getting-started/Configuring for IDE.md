---
category: getting-started
categoryindex: 1
index: 2
---

# Configuring for the IDE

## Visual Studio Code

In order to configure analyzers for VSCode, you will need to update your project's `.vscode/settings.json` file or your user settings. You should need the settings shown below.

```json
{
  "FSharp.enableAnalyzers": true,
  "FSharp.analyzersPath": ["path/to/analyzers/directory"]
}
```

📓 Note: Issue created [here](https://github.com/ionide/FsAutoComplete/issues/1350) regarding analyzers & SDK mismatches in the logs

After saving your new settings, make sure to restart VSCode. Once VSCode restarts, you should be able to test and see if the analyzers are working by opening a F# file in your workspace and entering the following code

![Analyzers Inline Warning](../../images/analyzers-inline-warning.png)

[Next]({{fsdocs-next-page-link}})