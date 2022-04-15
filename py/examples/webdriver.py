from eywa import Task
import time
from selenium import webdriver

task=Task()

task.error("Some shit happened")
task.info("Normal as usual")
task.warn("Uuuu you should be scared")
task.update_task()
# task.close(task.ERROR)

task.update_task(status=task.PROCESSING)
task.info("Opening Chrome browser")
browser = webdriver.Chrome()
task.info("Chrome opened")
task.info("Navigation to index.hr")
browser.get("http://www.index.hr")
task.info("Index.hr visible")

time.sleep(10)

browser.close()
task.info("Browser closed")
task.report("Everything went just fine",{'pici':'mici'})
task.close(task.SUCCESS)
