import { TalismanOptions } from "./TalismanOptions";

//------------------------------------------------------------------------------
// main
//------------------------------------------------------------------------------
async function main(argv: string[]) {

    process.on("SIGINT", async () => {
        console.log("*** Process was interrupted! ***")
        process.exit(1);
    });

    try {
        const options = new TalismanOptions(argv);
       
        if(options.showHelp)
        {
            showHelp(options);
            return 0;
        }

        if(options.badArgs.length > 0)
        {
            console.log("ERROR: Bad arguments: ");
            options.badArgs.forEach(arg => console.log(`  ${arg}`));
            process.exit();
        }   

        console.log("hi");
        return 0;
    } catch (error) {
        console.error(error.stack);
        return 1;
    } 
}

//------------------------------------------------------------------------------
// show help
//------------------------------------------------------------------------------
function showHelp(options: TalismanOptions)
{
    if(!options.helpOption)   
    {
        console.log("Talisman help")
    }
}

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

main(process.argv.slice(2))
    .then(status => {
        //console.log(`Exiting with status: ${status}`)
        process.exit(status);
    });

