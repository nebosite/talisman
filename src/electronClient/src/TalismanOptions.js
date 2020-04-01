"use strict";
exports.__esModule = true;
//------------------------------------------------------------------------------
// Simple Class for thinking about options
//------------------------------------------------------------------------------
var TalismanOptions = /** @class */ (function () {
    //------------------------------------------------------------------------------
    // ctor
    //------------------------------------------------------------------------------
    function TalismanOptions(argv) {
        this.showHelp = false;
        this.helpOption = undefined;
        this.badArgs = new Array();
        if (argv.length == 0) {
            this.showHelp = true;
            return;
        }
        // commandline is -name=value  (or /name=value), value is optional
        var argument = this.getArgParts(argv[0]);
        switch (argument.name) {
            case "h":
            case "help":
            case "?":
                this.processHelp(argv.slice(1));
                break;
            default:
                this.badArgs.push(argv[0]);
                break;
        }
    }
    //------------------------------------------------------------------------------
    // break -FOO=Bar into {name:"foo", value: "Bar"}
    //------------------------------------------------------------------------------
    TalismanOptions.prototype.getArgParts = function (argument) {
        var trimmed = argument.replace(/^([-/]*)/, "");
        var parts = trimmed.split('=', 2);
        if (parts.length == 2)
            parts[0] = parts[0].toLowerCase();
        return { name: parts[0], value: parts.length == 2 ? parts[1] : undefined };
    };
    //------------------------------------------------------------------------------
    // ctor
    //------------------------------------------------------------------------------
    TalismanOptions.prototype.processHelp = function (argv) {
        this.showHelp = true;
        if (argv.length > 0) {
            this.helpOption = this.getArgParts(argv[0]).name;
        }
    };
    return TalismanOptions;
}());
exports.TalismanOptions = TalismanOptions;
