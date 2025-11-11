/**
 * Report Test Suite
 * 
 * Comprehensive tests for all report functionality
 * Run this to validate your report implementation
 */

import eywa from '../../src/index.js';

// Mock functions for testing
const originalGetTask = eywa.get_task;
const originalGraphql = eywa.graphql;
let testResults = [];

eywa.get_task = async () => ({ euuid: 'test-task-uuid-123' });

let lastReport = null;
eywa.graphql = async (query, variables) => {
  lastReport = variables?.report;
  return {
    data: {
      stackTaskReport: {
        euuid: 'test-report-uuid',
        message: variables?.report?.message || 'Test',
        has_card: variables?.report?.has_card || false,
        has_table: variables?.report?.has_table || false,
        has_image: variables?.report?.has_image || false
      }
    }
  };
};

async function runTest(name, testFn) {
  try {
    await testFn();
    testResults.push({ name, status: '✅ PASS' });
    console.log(`✅ ${name}`);
  } catch (error) {
    testResults.push({ name, status: '❌ FAIL', error: error.message });
    console.log(`❌ ${name}: ${error.message}`);
  }
}

async function main() {
  console.log('🧪 Running Report Test Suite\\n');
  
  // Test 1: Basic card report
  await runTest('Card Report', async () => {
    await eywa.report("Test", { data: { card: "# Test" } });
    if (!lastReport?.has_card) throw new Error('has_card flag not set');
  });
  
  // Test 2: Table report
  await runTest('Table Report', async () => {
    await eywa.report("Test", {
      data: { tables: { "Test": { headers: ["A"], rows: [["1"]] } } }
    });
    if (!lastReport?.has_table) throw new Error('has_table flag not set');
  });
  
  // Test 3: Image report
  await runTest('Image Report', async () => {
    const validBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
    await eywa.report("Test", { image: validBase64 });
    if (!lastReport?.has_image) throw new Error('has_image flag not set');
  });
  
  // Test 4: Complete report
  await runTest('Complete Report', async () => {
    const validBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
    await eywa.report("Test", {
      data: {
        card: "# Test",
        tables: { "Test": { headers: ["A"], rows: [["1"]] } }
      },
      image: validBase64
    });
    if (!lastReport?.has_card || !lastReport?.has_table || !lastReport?.has_image) {
      throw new Error('Not all flags set for complete report');
    }
  });
  
  // Test 5: Invalid base64 handling
  await runTest('Invalid Base64 Error', async () => {
    try {
      await eywa.report("Test", { image: "invalid-base64!" });
      throw new Error('Should have thrown error for invalid base64');
    } catch (error) {
      if (!error.message.includes('Invalid base64')) {
        throw new Error('Wrong error type for invalid base64');
      }
    }
  });
  
  // Results summary
  const passed = testResults.filter(r => r.status.includes('PASS')).length;
  const failed = testResults.filter(r => r.status.includes('FAIL')).length;
  
  console.log(`\\n📊 Test Results: ${passed}/${testResults.length} passed`);
  
  if (failed === 0) {
    console.log('🎉 All tests passed! Report functionality is working correctly.');
  } else {
    console.log(`⚠️ ${failed} tests failed. Check implementation.`);
    testResults.filter(r => r.status.includes('FAIL')).forEach(r => {
      console.log(`   ❌ ${r.name}: ${r.error}`);
    });
  }
  
  // Restore original functions
  eywa.get_task = originalGetTask;
  eywa.graphql = originalGraphql;
}

main().catch(console.error);
