/// <summary>
/// EYWA Task Report Example - Exactly matching Node.js implementation
/// 
/// Demonstrates task reporting capabilities following the Node.js pattern exactly:
/// - Simple markdown cards
/// - Multi-table reports  
/// - Base64 image attachments
/// - NO metadata (unless schema supports it)
/// </summary>

using EywaClient;
using EywaClient.Core;

var eywa = new Eywa();

try
{
    // Start EYWA communication
    eywa.OpenPipe();

    // Get current task info
    var task = await eywa.Tasks.GetTaskAsync();
    Console.WriteLine($"Running task: {task.GetValueOrDefault("euuid")}");

    // Example 1: Simple markdown card report (matches Node.js example)
    await eywa.Tasks.ReportAsync("Daily Summary", new ReportOptions
    {
        Data = new ReportData
        {
            Card = """
                # Success! 
                Processed **1,000 records** with 0 errors.
                """
        }
    });

    // Example 2: Multi-table report (matches Node.js pattern)
    await eywa.Tasks.ReportAsync("Performance Analysis", new ReportOptions
    {
        Data = new ReportData
        {
            Card = """
                # Performance Report
                ## Summary
                System performance exceeded targets by **15%**.
                
                ### Key Metrics
                - **Throughput:** 1,200 req/sec  
                - **Latency:** P95 < 100ms
                - **Errors:** 0.01%
                """,
            Tables = new Dictionary<string, TableData>
            {
                ["Endpoint Performance"] = new TableData
                {
                    Headers = ["Endpoint", "Requests", "Avg Response", "Error Rate"],
                    Rows = new object[][]
                    {
                        ["/api/users", "15,420", "45ms", "0.01%"],
                        ["/api/orders", "8,930", "123ms", "0.02%"],
                        ["/api/reports", "2,100", "890ms", "0.00%"]
                    }
                },
                ["System Health"] = new TableData
                {
                    Headers = ["Service", "Uptime", "Response Time", "Status"],
                    Rows = new object[][]
                    {
                        ["API Gateway", "99.9%", "85ms", "Healthy"],
                        ["Database", "100%", "12ms", "Healthy"],
                        ["Cache Layer", "99.8%", "3ms", "Healthy"]
                    }
                }
            }
        }
    });

    // Example 3: Report with base64 image (matches Node.js pattern)
    var sampleChart = GenerateSampleBase64Image();
    await eywa.Tasks.ReportAsync("Visual Analysis", new ReportOptions
    {
        Data = new ReportData
        {
            Card = """
                # Monthly Trends
                
                Chart shows **significant growth** in Q4 2024.
                
                **Key Insights:**
                - Revenue up 23% YoY
                - User engagement improved 15%
                - Mobile traffic now 65% of total
                """,
            Tables = new Dictionary<string, TableData>
            {
                ["Monthly Metrics"] = new TableData
                {
                    Headers = ["Month", "Revenue", "Users", "Conversion"],
                    Rows = new object[][]
                    {
                        ["October", "$125K", "8,450", "3.2%"],
                        ["November", "$156K", "9,230", "3.8%"],
                        ["December", "$189K", "11,200", "4.1%"]
                    }
                }
            }
        },
        Image = sampleChart
    });

    // Example 4: Business report (NO METADATA - matches Node.js approach)
    await eywa.Tasks.ReportAsync("Business Review", new ReportOptions
    {
        Data = new ReportData
        {
            Card = """
                # Q4 2024 Business Report
                
                ## Executive Summary
                **Outstanding quarter** with record-breaking performance across all metrics.
                
                ### Key Achievements
                - **Revenue Growth:** 34% QoQ  
                - **Customer Acquisition:** 2,450 new customers
                - **Customer Satisfaction:** 97% (NPS: 68)
                - **Churn Rate:** Reduced to 1.2%
                
                ### Performance Highlights
                - Exceeded all quarterly targets
                - Launched 3 major product features
                - Expanded to 2 new markets
                - Team grew by 15 people
                """,
            Tables = new Dictionary<string, TableData>
            {
                ["Regional Performance"] = new TableData
                {
                    Headers = ["Region", "Revenue", "Growth", "Customers", "Target"],
                    Rows = new object[][]
                    {
                        ["North America", "$567K", "28%", "3,250", "✅ Met"],
                        ["Europe", "$423K", "35%", "2,890", "✅ Exceeded"],
                        ["Asia Pacific", "$234K", "67%", "1,450", "✅ Exceeded"],
                        ["Latin America", "$156K", "45%", "890", "✅ Met"]
                    }
                },
                ["Product Performance"] = new TableData
                {
                    Headers = ["Product", "Usage", "Revenue", "Growth", "Satisfaction"],
                    Rows = new object[][]
                    {
                        ["Core Platform", "89%", "$890K", "25%", "96%"],
                        ["Analytics Suite", "67%", "$340K", "78%", "94%"],
                        ["Mobile App", "78%", "$230K", "45%", "91%"],
                        ["API Services", "34%", "$120K", "156%", "88%"]
                    }
                },
                ["Team Metrics"] = new TableData
                {
                    Headers = ["Department", "Headcount", "Productivity", "Satisfaction", "Retention"],
                    Rows = new object[][]
                    {
                        ["Engineering", "45", "142%", "94%", "96%"],
                        ["Product", "12", "156%", "97%", "100%"],
                        ["Sales", "23", "134%", "91%", "91%"],
                        ["Marketing", "8", "189%", "95%", "100%"],
                        ["Support", "15", "145%", "93%", "93%"]
                    }
                }
            }
        }
        // NO METADATA - following Node.js example exactly
    });

    Console.WriteLine("✅ All task reports generated successfully!");
    
    await eywa.Tasks.CloseTaskAsync(Status.Success);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    await eywa.Logger.ErrorAsync($"Task failed: {ex.Message}", new { exception = ex.ToString() });
    await eywa.Tasks.CloseTaskAsync(Status.Error);
}

/// <summary>
/// Generate a simple base64 image for demonstration
/// This creates a tiny 1x1 pixel PNG image encoded as base64
/// </summary>
static string GenerateSampleBase64Image()
{
    // This is a 1x1 transparent PNG image encoded as base64
    // In practice, you'd read an actual chart image file
    return "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
}
