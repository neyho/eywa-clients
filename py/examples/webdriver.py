from eywa import Task
from selenium import webdriver

task=Task()

task.error("Some shit happened")
task.info("Normal as usual")
task.warn("Uuuu you should be scared")
task.update_task()
# task.close(task.ERROR)

print('Evo nekog texta')
task.update_task(status=task.PROCESSING)
task.info("Opening Chrome browser")
browser = webdriver.Chrome()
task.info("Chrome opened")
task.info("Navigation to www.google.com")
browser.get("http://www.google.com")
task.info("Google visible")
browser.close()
task.info("Browser closed")
task.report("Everything went just fine",{'hanky':'dory'})
# task.close(task.SUCCESS)
