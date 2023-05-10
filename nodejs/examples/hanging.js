import eywa from 'eywa-reacher-client'


function execute() {
    eywa.info("start");
    setInterval(() => {
        eywa.info("repeat");
    }, 10000);
}

execute();
