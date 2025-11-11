/**
 * Test Suite for EYWA Task Reports
 * 
 * Tests the enhanced reporting functionality including:
 * - Data validation
 * - Flag generation
 * - Error handling
 * - Base64 validation
 */

import eywa from '../src/index.js';

// Mock the get_task function for testing
const originalGetTask = eywa.get_task;
eywa.get_task = async () => ({ euuid: 'test-task-uuid-123' });

// Mock the graphql function for testing  
const originalGraphql = eywa.graphql;
let lastGraphqlCall = null;
eywa.graphql = async (query, variables) => {
  lastGraphqlCall = { query, variables };
  return {
    data: {
      stackTaskReport: {
        euuid: 'test-report-uuid',
        message: variables.report.message,
        has_card: variables.report.has_card,
        has_table: variables.report.has_table,
        has_image: variables.report.has_image,
        created_on: new Date().toISOString()
      }
    }
  };
};

async function runTests() {
  console.log('🧪 Running EYWA Task Report Tests...\n');
  
  let passedTests = 0;
  let totalTests = 0;
  
  // Test 1: Basic card report
  totalTests++;
  try {
    await eywa.report("Test Message", {
      data: {
        card: "# Test Card\nThis is a test."
      }
    });
    
    const reportData = lastGraphqlCall.variables.report;
    if (reportData.has_card === true && reportData.has_table === false && reportData.has_image === false) {
      console.log('✅ Test 1: Basic card report - PASSED');
      passedTests++;
    } else {
      console.log('❌ Test 1: Basic card report - FAILED (incorrect flags)');
    }
  } catch (error) {
    console.log('❌ Test 1: Basic card report - FAILED:', error.message);
  }
  
  // Test 2: Table report
  totalTests++;
  try {
    await eywa.report("Table Test", {
      data: {
        tables: {
          "Test Table": {
            headers: ["Col1", "Col2"],
            rows: [["A", "B"], ["C", "D"]]
          }
        }
      }
    });
    
    const reportData = lastGraphqlCall.variables.report;
    if (reportData.has_card === false && reportData.has_table === true && reportData.has_image === false) {
      console.log('✅ Test 2: Table report - PASSED');
      passedTests++;
    } else {
      console.log('❌ Test 2: Table report - FAILED (incorrect flags)');
    }
  } catch (error) {
    console.log('❌ Test 2: Table report - FAILED:', error.message);
  }
  
  // Test 3: Card + Table + Image report
  totalTests++;
  try {
    const validBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
    
    await eywa.report("Complete Test", {
      data: {
        card: "# Test",
        tables: {
          "Test": {
            headers: ["A"],
            rows: [["1"]]
          }
        }
      },
      image: validBase64
    });
    
    const reportData = lastGraphqlCall.variables.report;
    if (reportData.has_card === true && reportData.has_table === true && reportData.has_image === true) {
      console.log('✅ Test 3: Complete report (card + table + image) - PASSED');
      passedTests++;
    } else {
      console.log('❌ Test 3: Complete report - FAILED (incorrect flags)');
    }
  } catch (error) {
    console.log('❌ Test 3: Complete report - FAILED:', error.message);
  }
  
  // Test 4: Empty card handling
  totalTests++;
  try {
    await eywa.report("Empty Card Test", {
      data: {
        card: ""  // Empty string should not set has_card flag
      }
    });
    
    const reportData = lastGraphqlCall.variables.report;
    if (reportData.has_card === false) {
      console.log('✅ Test 4: Empty card handling - PASSED');
      passedTests++;
    } else {
      console.log('❌ Test 4: Empty card handling - FAILED (has_card should be false)');
    }
  } catch (error) {
    console.log('❌ Test 4: Empty card handling - FAILED:', error.message);
  }
  
  // Test 5: Invalid base64 error
  totalTests++;
  try {
    await eywa.report("Invalid Image Test", {
      image: "invalid-base64-string!"
    });
    console.log('❌ Test 5: Invalid base64 error - FAILED (should have thrown error)');
  } catch (error) {
    if (error.message.includes('Invalid base64')) {
      console.log('✅ Test 5: Invalid base64 error - PASSED');
      passedTests++;
    } else {
      console.log('❌ Test 5: Invalid base64 error - FAILED (wrong error message)');
    }
  }
  
  // Test 6: Invalid table structure error
  totalTests++;
  try {
    await eywa.report("Invalid Table Test", {
      data: {
        tables: {
          "Bad Table": {
            headers: ["Col1", "Col2"],
            rows: [["Value1"]] // Missing second column
          }
        }
      }
    });
    console.log('❌ Test 6: Invalid table structure - FAILED (should have thrown error)');
  } catch (error) {
    if (error.message.includes('columns')) {
      console.log('✅ Test 6: Invalid table structure error - PASSED');
      passedTests++;
    } else {
      console.log('❌ Test 6: Invalid table structure error - FAILED (wrong error message)');
    }
  }
  
  // Test 7: Message only report
  totalTests++;
  try {
    await eywa.report("Message Only");
    
    const reportData = lastGraphqlCall.variables.report;
    if (reportData.has_card === false && reportData.has_table === false && reportData.has_image === false) {
      console.log('✅ Test 7: Message-only report - PASSED');
      passedTests++;
    } else {
      console.log('❌ Test 7: Message-only report - FAILED (all flags should be false)');
    }
  } catch (error) {
    console.log('❌ Test 7: Message-only report - FAILED:', error.message);
  }
  
  // Test 8: Metadata handling
  totalTests++;
  try {
    await eywa.report("Metadata Test", {
      metadata: {
        version: "1.0",
        system: "test"
      }
    });
    
    const reportData = lastGraphqlCall.variables.report;
    if (reportData.metadata && reportData.metadata.version === "1.0") {
      console.log('✅ Test 8: Metadata handling - PASSED');
      passedTests++;
    } else {
      console.log('❌ Test 8: Metadata handling - FAILED (metadata not preserved)');
    }
  } catch (error) {
    console.log('❌ Test 8: Metadata handling - FAILED:', error.message);
  }
  
  // Test Results Summary
  console.log(`\n📊 Test Results: ${passedTests}/${totalTests} tests passed`);
  
  if (passedTests === totalTests) {
    console.log('🎉 All tests passed! Task reporting functionality is working correctly.');
  } else {
    console.log(`⚠️  ${totalTests - passedTests} tests failed. Please review the implementation.`);
  }
  
  // Restore original functions
  eywa.get_task = originalGetTask;
  eywa.graphql = originalGraphql;
}

// Run tests if this file is executed directly
runTests().catch(console.error);

export { runTests };
