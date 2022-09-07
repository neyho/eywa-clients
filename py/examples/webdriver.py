import eywa
from selenium import webdriver

eywa.error("Some shit happened")
eywa.info("Normal as usual")
eywa.warn("Uuuu you should be scared")
eywa.update_task()
# task.close(task.ERROR)

print('Evo nekog texta')
eywa.update_task(status=eywa.PROCESSING)
eywa.info("Opening Chrome browser")
browser = webdriver.Chrome()
eywa.info("Chrome opened")
eywa.info("Navigation to www.google.com")
browser.get("http://www.google.com")
eywa.info("Google visible")
browser.close()
eywa.info("Browser closed")
eywa.report("Everything went just fine",{'hanky':'dory'})
# eywa.close(eywa.SUCCESS)
