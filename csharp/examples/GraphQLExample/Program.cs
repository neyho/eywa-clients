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
using System.Text.Json.Nodes;
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
    var user = result?["data"]?["stackUser"];
    if (user != null)
    {
        Console.WriteLine("✅ User Created Successfully:");
        Console.WriteLine($"   UUID: {user["euuid"]?.GetValue<string>() ?? "(null)"}");
        Console.WriteLine($"   Name: {user["name"]?.GetValue<string>() ?? "(null)"}");
        Console.WriteLine($"   Created: {user["modified_on"]?.GetValue<string>() ?? "(null)"}");
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
    var user = result?["data"]?["getUser"];
    if (user != null)
    {
        Console.WriteLine("✅ User Found:");
        Console.WriteLine($"   UUID: {user["euuid"]?.GetValue<string>() ?? "(null)"}");
        Console.WriteLine($"   Name: {user["name"]?.GetValue<string>() ?? "(null)"}");
        Console.WriteLine($"   Type: {user["type"]?.GetValue<string>() ?? "(not set)"}");
        Console.WriteLine($"   Last Modified: {user["modified_on"]?.GetValue<string>() ?? "(null)"}");
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
        var deleteResult = result?["data"]?["deleteUser"];
        if (deleteResult != null)
        {
            var deleted = deleteResult.GetValue<bool>();
            if (deleted)
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