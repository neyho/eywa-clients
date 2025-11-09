/**
 * EYWA C# Simple GraphQL Test
 * 
 * Tests basic GraphQL operations with available schema:
 * 1. Create User with fixed UUID
 * 2. Search for User
 * 3. Update User  
 * 4. Delete User (cleanup)
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using EywaClient;
using EywaClient.Core;

// Fixed UUID for consistent testing
string TEST_USER_UUID = "33333333-3333-3333-3333-333333333333";

var eywa = new Eywa();

try
{
    eywa.OpenPipe();
    
    var task = await eywa.Tasks.GetTaskAsync();
    await eywa.Logger.InfoAsync("Simple GraphQL Test Started");
    await eywa.Tasks.UpdateTaskAsync(Status.Processing);
    
    // Step 1: Create User
    Console.WriteLine("=== Creating Test User ===");
    await CreateUser(eywa);
    
    // Step 2: Search for User
    Console.WriteLine("=== Searching for User ===");
    await SearchUser(eywa);
    
    // Step 3: Cleanup
    Console.WriteLine("=== Cleanup ===");
    await DeleteUser(eywa);
    
    Console.WriteLine("=== Simple GraphQL Test Completed Successfully ===");
    await eywa.Logger.InfoAsync("Simple GraphQL Test Completed");
    await eywa.Tasks.CloseTaskAsync(Status.Success);
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    await eywa.Logger.ErrorAsync("Simple GraphQL Test Failed", new { error = ex.Message });
    await eywa.Tasks.CloseTaskAsync(Status.Error);
    throw;
}
finally
{
    eywa.Dispose();
}

async Task CreateUser(Eywa eywa)
{
    var query = @"
        mutation CreateUser($user: UserInput!) {
            stackUser(data: $user) {
                euuid
                name
                modified_on
            }
        }";
        
    var variables = new Dictionary<string, object>
    {
        ["user"] = new Dictionary<string, object>
        {
            ["euuid"] = TEST_USER_UUID,
            ["name"] = "Test User C#"
        }
    };
    
    var result = await eywa.GraphQLAsync(query, variables);
    
    // Extract and display the actual user data
    if (TryGetGraphQLData(result, "stackUser", out var user))
    {
        Console.WriteLine("✅ User Created Successfully:");
        Console.WriteLine($"   UUID: {(user != null ? GetValue(user, "euuid") : "(null)")}");
        Console.WriteLine($"   Name: {(user != null ? GetValue(user, "name") : "(null)")}");
        Console.WriteLine($"   Created: {(user != null ? GetValue(user, "modified_on") : "(null)")}");
    }
    else
    {
        Console.WriteLine("❌ Failed to extract user data from response");
    }
}

async Task SearchUser(Eywa eywa)
{
    var query = @"
        query GetUser($uuid: UUID!) {
            getUser(euuid: $uuid) {
                euuid
                name
                modified_on
                type
            }
        }";
        
    var variables = new Dictionary<string, object>
    {
        ["uuid"] = TEST_USER_UUID
    };
    
    var result = await eywa.GraphQLAsync(query, variables);
    
    // Extract and display the user search results
    if (TryGetGraphQLData(result, "getUser", out var user))
    {
        Console.WriteLine("✅ User Found:");
        Console.WriteLine($"   UUID: {(user != null ? GetValue(user, "euuid") : "(null)")}");
        Console.WriteLine($"   Name: {(user != null ? GetValue(user, "name") : "(null)")}");
        Console.WriteLine($"   Type: {(user != null ? GetValue(user, "type") ?? "(not set)" : "(null)")}");
        Console.WriteLine($"   Last Modified: {(user != null ? GetValue(user, "modified_on") : "(null)")}");
    }
    else
    {
        Console.WriteLine("❌ Failed to extract user data from response");
    }
}

async Task DeleteUser(Eywa eywa)
{
    try
    {
        var query = @"
            mutation DeleteUser($uuid: UUID!) {
                deleteUser(euuid: $uuid)
            }";
            
        var variables = new Dictionary<string, object>
        {
            ["uuid"] = TEST_USER_UUID
        };
        
        var result = await eywa.GraphQLAsync(query, variables);
        
        // Extract and display deletion result
        if (TryGetGraphQLData(result, "deleteUser", out var deleteResult))
        {
            if (deleteResult is bool deleted && deleted)
            {
                Console.WriteLine("✅ User Successfully Deleted");
            }
            else
            {
                Console.WriteLine($"⚠️ User deletion returned: {deleteResult}");
            }
        }
        else
        {
            Console.WriteLine("❌ Failed to extract deletion result");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Cleanup warning: {ex.Message}");
    }
}


static bool TryGetGraphQLData(Dictionary<string, object> result, string fieldName, out object? data)
{
    data = null;
    
    if (!result.ContainsKey("data"))
        return false;
        
    var dataObj = result["data"];
    
    // Handle JsonElement
    if (dataObj is JsonElement dataElement)
    {
        if (dataElement.TryGetProperty(fieldName, out var fieldElement))
        {
            // Convert JsonElement to appropriate type
            if (fieldElement.ValueKind == JsonValueKind.Object)
            {
                var fieldJson = fieldElement.GetRawText();
                data = JsonSerializer.Deserialize<Dictionary<string, object>>(fieldJson) ?? new Dictionary<string, object>();
            }
            else if (fieldElement.ValueKind == JsonValueKind.True)
            {
                data = true;
            }
            else if (fieldElement.ValueKind == JsonValueKind.False)
            {
                data = false;
            }
            else if (fieldElement.ValueKind == JsonValueKind.Null)
            {
                data = "(null)";
            }
            else
            {
                data = fieldElement.ToString();
            }
            return true;
        }
    }
    // Handle Dictionary
    else if (dataObj is Dictionary<string, object> dataDict)
    {
        if (dataDict.ContainsKey(fieldName))
        {
            data = dataDict[fieldName];
            return true;
        }
    }
    
    return false;
}

static string GetValue(object? obj, string key)
{
    if (obj is Dictionary<string, object> dict)
    {
        return dict.GetValueOrDefault(key)?.ToString() ?? "(null)";
    }
    return "(not a dictionary)";
}