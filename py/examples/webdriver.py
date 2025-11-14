#!/usr/bin/env python3
"""
EYWA WebDriver Automation Demo

Demonstrates web automation with Selenium WebDriver integrated with EYWA:
- Chrome browser automation
- Page navigation and interaction
- Element finding and manipulation
- Structured reporting of automation results

Usage: eywa run -c "python examples/webdriver.py"

Requirements:
- selenium
- chromedriver (must be in PATH)
"""

import sys
import os

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'src'))

import eywa
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

# Start EYWA automation task
eywa.open_pipe()

try:
    eywa.info("🚀 Starting web automation demo")
    eywa.update_task(status=eywa.PROCESSING)
    
    # Configure Chrome options
    chrome_options = Options()
    chrome_options.add_argument("--headless")  # Run headless for automation
    chrome_options.add_argument("--no-sandbox")
    chrome_options.add_argument("--disable-dev-shm-usage")
    
    eywa.info("Opening Chrome browser")
    browser = webdriver.Chrome(options=chrome_options)
    
    try:
        eywa.info("Navigating to Google")
        browser.get("https://www.google.com")
        
        # Wait for page to load and verify title
        WebDriverWait(browser, 10).until(
            lambda driver: "Google" in driver.title
        )
        eywa.info(f"✅ Page loaded: {browser.title}")
        
        # Find search box and perform search
        search_box = browser.find_element(By.NAME, "q")
        search_box.send_keys("EYWA automation")
        search_box.submit()
        
        # Wait for results
        WebDriverWait(browser, 10).until(
            EC.presence_of_element_located((By.ID, "search"))
        )
        
        eywa.info("✅ Search completed successfully")
        
        # Report results
        results_count = len(browser.find_elements(By.CSS_SELECTOR, "div.g"))
        eywa.report(
            "Web automation completed successfully",
            {
                "page_title": browser.title,
                "search_results_found": results_count,
                "automation_status": "success",
                "search_term": "EYWA automation"
            }
        )
        
    finally:
        browser.quit()
        eywa.info("Browser closed")
    
    eywa.close_task(eywa.SUCCESS)
    
except Exception as e:
    eywa.error(f"❌ Automation failed: {e}")
    eywa.close_task(eywa.ERROR)
