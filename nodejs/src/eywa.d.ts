export function send_request(data:Any): Any
export function send_notification(data:Any): Any
export function register_handler(handler:Function): Any
export function open_pipe(): void
export function close_pipe(): void
export function log(record:Any): void
export function error(message:String, data:Any): void
export function info(message:String, data:Any): void
export function warn(message:String, data:Any): void
export function debug(message:String, data:Any): void
export function trace(message:String, data:Any): void
export function report(message:String, data:Any, image: Any): void
export function update_task(status: String): void
export function return_task(status: String): void
export function graphql(query:Any, variables:Any): void
