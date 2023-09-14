// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Net.Http.Headers;

namespace ManageSqlDatabaseInElasticPool
{
    /**
     * Azure Storage sample for managing SQL Database -
     *  - Create a SQL Server with elastic pool and 2 databases
     *  - Create another database and add it to elastic pool through database update
     *  - Create one more database and add it to elastic pool through elastic pool update.
     *  - List and print databases in the elastic pool
     *  - Remove a database from elastic pool.
     *  - List and print elastic pool activities
     *  - List and print elastic pool database activities
     *  - Add another elastic pool in existing SQL Server.
     *  - Delete database, elastic pools and SQL Server
     */
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;

        public static async Task RunSample(ArmClient client)
        {
            try
            {
                //Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                //Create a resource group in the EastUS region
                string rgName = Utilities.CreateRandomName("rgSQLServer");
                Utilities.Log("Creating resource group...");
                var rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log($"Created a resource group with name: {resourceGroup.Data.Name} ");

                // ============================================================
                // Create a SQL Server, with 2 firewall rules.

                Utilities.Log("Creating a SQL Server with 2 firewall rules");
                string sqlServerName = Utilities.CreateRandomName("sqlserver-elasticpooltest");
                Utilities.Log("Creating SQL Server...");
                SqlServerData sqlData = new SqlServerData(AzureLocation.EastUS)
                {
                    AdministratorLogin = "sqladmin" + sqlServerName,
                    AdministratorLoginPassword = Utilities.CreatePassword()
                };
                var sqlServerLro = await resourceGroup.GetSqlServers().CreateOrUpdateAsync(WaitUntil.Completed, sqlServerName, sqlData);
                SqlServerResource sqlServer = sqlServerLro.Value;
                Utilities.Log($"Created a SQL Server with name: {sqlServer.Data.Name} ");

                string FirewallRule1stName = Utilities.CreateRandomName("firewallrule1st-");
                Utilities.Log("Creating 2 firewall rules...");
                SqlFirewallRuleData FirewallRule1stData = new SqlFirewallRuleData()
                {
                    StartIPAddress = "10.2.0.1",
                    EndIPAddress = "10.2.0.10"
                };
                var FirewallRule1stLro = await sqlServer.GetSqlFirewallRules().CreateOrUpdateAsync(WaitUntil.Completed, FirewallRule1stName, FirewallRule1stData);
                SqlFirewallRuleResource FirewallRule1st = FirewallRule1stLro.Value;
                Utilities.Log($"Created first firewall rule with name {FirewallRule1st.Data.Name}");

                string FirewallRule2ndName = Utilities.CreateRandomName("firewallrule2nd-");
                SqlFirewallRuleData FirewallRule2ndData = new SqlFirewallRuleData()
                {
                    StartIPAddress = "10.0.0.1",
                    EndIPAddress = "10.0.0.10"
                };
                var FirewallRule2ndLro = await sqlServer.GetSqlFirewallRules().CreateOrUpdateAsync(WaitUntil.Completed, FirewallRule2ndName, FirewallRule2ndData);
                SqlFirewallRuleResource FirewallRule2nd = FirewallRule2ndLro.Value;
                Utilities.Log($"Created second firewall rule with name {FirewallRule2nd.Data.Name}");

                Utilities.Log("Creating a elastic pool of SQL Server...");
                string elasticPoolName = Utilities.CreateRandomName("myElasticPool");
                var elasticPooldata = new ElasticPoolData(AzureLocation.EastUS)
                {
                    Sku = new SqlSku("StandardPool")
                };
                var elasticPool = (await sqlServer.GetElasticPools().CreateOrUpdateAsync(WaitUntil.Completed, elasticPoolName, elasticPooldata)).Value;
                Utilities.Log($"Created a elastic pool of SQL Server with name {elasticPool.Data.Name}");

                Utilities.Log("Creating 2 database of SQL Server...");
                string database1Name = Utilities.CreateRandomName("myDatabase1");
                SqlDatabaseData databaseData = new SqlDatabaseData(AzureLocation.EastUS) 
                {
                    ElasticPoolId = elasticPool.Id
                };
                SqlDatabaseResource database1 = (await sqlServer.GetSqlDatabases().CreateOrUpdateAsync(WaitUntil.Completed, database1Name, databaseData)).Value;
                Utilities.Log($"Created first database of SQL Server with name {database1.Data.Name}");

                string database2Name = Utilities.CreateRandomName("myDatabase2");
                SqlDatabaseResource database2 = (await sqlServer.GetSqlDatabases().CreateOrUpdateAsync(WaitUntil.Completed, database2Name, databaseData)).Value;
                Utilities.Log($"Created second database of SQL Server with name {database2.Data.Name}");
                // ============================================================
                // List and prints the elastic pools
                foreach (var elasticPoolInList in sqlServer.GetElasticPools().ToList())
                {
                    Utilities.Log($"List and prints the elastic pools with elastic pools name:{elasticPoolInList.Data.Name}");
                }

                // ============================================================
                // Get and prints the elastic pool
                var getElasticPool = await sqlServer.GetElasticPoolAsync(elasticPoolName);
                Utilities.Log($"Get and prints the elastic pool with name: {getElasticPool.Value.Data.Name}");

                // ============================================================
                // Change DTUs in the elastic pools.
                Utilities.Log("Change DTUs in the elastic pools...");
                var changeData = new ElasticPoolPatch()
                {
                    Sku = new SqlSku("StandardPool")
                    {
                        Tier = "Standard",
                        Capacity = 200,
                    },                    
                    PerDatabaseSettings = new ElasticPoolPerDatabaseSettings()
                    {
                        MaxCapacity = 50,
                        MinCapacity = 10
                    },
                };
                elasticPool = (await elasticPool.UpdateAsync(WaitUntil.Completed, changeData)).Value;
                Utilities.Log($"Change DTUs in the elastic pools {elasticPool.Data.Name}");

                Utilities.Log("Start ------- Current databases in the elastic pool");
                foreach (var databaseInElasticPool in elasticPool.GetDatabases().ToList())
                {
                    Utilities.Log($"Current databases in the elastic pool with databasename: {databaseInElasticPool.Data.Name}");
                }
                Utilities.Log("End --------- Current databases in the elastic pool");

                // ============================================================
                // Create a Database in SQL server created above.
                Utilities.Log("Creating a database");

                var newSqlDBName = Utilities.CreateRandomName("myNewDatabase");
                var newSqlDBData = new SqlDatabaseData(AzureLocation.EastUS) { };
                var newSqlDB = (await sqlServer.GetSqlDatabases().CreateOrUpdateAsync(WaitUntil.Completed, newSqlDBName, newSqlDBData)).Value;
                Utilities.Log($"Created a database with name: {newSqlDB.Data.Name}");

                Utilities.Log("Start ------- Current databases in the elastic pool");
                foreach (var databaseInElasticPool in elasticPool.GetDatabases().ToList())
                {
                    Utilities.Log($"Current databases in the elastic pool with databasename: {databaseInElasticPool.Data.Name}");
                }
                Utilities.Log("End --------- Current databases in the elastic pool");

                // ============================================================
                // Move newly created database to the pool.
                Utilities.Log("Updating a database");
                var updateDBData = new SqlDatabasePatch()
                {
                    ElasticPoolId = elasticPool.Id
                };
                newSqlDB = (await newSqlDB.UpdateAsync(WaitUntil.Completed, updateDBData)).Value;
                Utilities.Log($"Updated a database with name:{newSqlDB.Data.Name} ");

                // ============================================================
                // Create another database and move it in elastic pool as update to the elastic pool.
                var anotherDatabaseName = Utilities.CreateRandomName("myAnotherDatabase");
                var anotherDatabaseData = new SqlDatabaseData(AzureLocation.EastUS) { };
                var anotherDatabase = (await sqlServer.GetSqlDatabases().CreateOrUpdateAsync(WaitUntil.Completed, anotherDatabaseName, anotherDatabaseData)).Value;

                // ============================================================
                // Update the elastic pool to have newly created database.
                Utilities.Log("Update the elastic pool to have newly created database");
                var updateAnotherElasticPoolData = new SqlDatabasePatch()
                {
                    ElasticPoolId = elasticPool.Id
                };
                anotherDatabase = (await anotherDatabase.UpdateAsync(WaitUntil.Completed, updateAnotherElasticPoolData)).Value;

                Utilities.Log("Start ------- Current databases in the elastic pool");
                foreach (var databaseInElasticPool in elasticPool.GetDatabases().ToList())
                {
                    Utilities.Log($"Current databases in the elastic pool with databasename: {databaseInElasticPool.Data.Name}");
                }
                Utilities.Log("End --------- Current databases in the elastic pool");

                // ============================================================
                // Remove the database from the elastic pool.
                Utilities.Log("Removing the database from the pool...");
                var removeElasticPoolDBData = new SqlDatabasePatch()
                {
                    Sku = new SqlSku("S2")
                    {
                        Tier = "Standard",
                        Capacity = 50
                    },
                    ElasticPoolId = null
                };
                var removeElasticPoolDB = (await anotherDatabase.UpdateAsync(WaitUntil.Completed, removeElasticPoolDBData)).Value;
                Utilities.Log($"Removed the database from the pool with database name :{removeElasticPoolDB.Data.Name}");

                Utilities.Log("Start ------- Current databases in the elastic pool");
                foreach (var databaseInElasticPool in elasticPool.GetDatabases().ToList())
                {
                    Utilities.Log($"Current databases in the elastic pool with databasename: {databaseInElasticPool.Data.Name}");
                }
                Utilities.Log("End --------- Current databases in the elastic pool");

                // ============================================================
                // Get list of elastic pool's activities and print the same.
                Utilities.Log("Start ------- Activities in a elastic pool");
                foreach (var activity in elasticPool.GetElasticPoolActivities().ToList())
                {
                    Utilities.Log($"Activities in a elastic pool with id: {activity.Name}");
                }
                Utilities.Log("End ------- Activities in a elastic pool");

                 //============================================================
                 //Get list of elastic pool's database activities and print the same.

                Utilities.Log("Start ------- Activities in a elastic pool");
                foreach (var databaseActivity in elasticPool.GetElasticPoolDatabaseActivities().ToList())
                {
                    Utilities.Log($"Activities in a elastic pool with databasename: {databaseActivity.DatabaseName}");
                }
                Utilities.Log("End ------- Activities in a elastic pool");

                // ============================================================
                // List databases in the sql server and delete the same.
                Utilities.Log("List and delete all databases from SQL Server");
                foreach (var databaseInServer in sqlServer.GetSqlDatabases().ToList())
                {
                    Utilities.Log($"List and delete database with name {databaseInServer.Data.Name}");
                    try
                    {
                        await databaseInServer.DeleteAsync(WaitUntil.Completed);
                    }
                    catch (Exception ex)
                    {
                        Utilities.Log($"Failed to delete SQL database with database name: {databaseInServer.Data.Name}; {ex.Message}");
                    }
                }

                // ============================================================
                // Create another elastic pool in SQL Server
                Utilities.Log("Creating ElasticPool in existing SQL Server...");
                string elasticPool2Name = Utilities.CreateRandomName("secondElasticPool");
                var elasticPool2data = new ElasticPoolData(AzureLocation.EastUS)
                {
                    Sku = new SqlSku("StandardPool")
                    {
                        Tier = "Standard"
                    }
                };
                var elasticPool2 = (await sqlServer.GetElasticPools().CreateOrUpdateAsync(WaitUntil.Completed, elasticPool2Name, elasticPool2data)).Value;

                Utilities.Log($"Created ElasticPool in existing SQL Server with name {elasticPool2.Data.Name}");
                // ============================================================
                // Deletes the elastic pool.
                Utilities.Log("Delete the elastic pool from the SQL Server");
                await (await sqlServer.GetElasticPoolAsync(elasticPoolName)).Value.DeleteAsync(WaitUntil.Completed);
                await (await sqlServer.GetElasticPoolAsync(elasticPool2Name)).Value.DeleteAsync(WaitUntil.Completed);

                // ============================================================
                // Delete the SQL Server.
                Utilities.Log("Deleting a Sql Server...");
                await sqlServer.DeleteAsync(WaitUntil.Completed);
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group...");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId.Name}");
                    }
                }
                catch (Exception e)
                {
                    Utilities.Log(e);
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                await RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e.ToString());
            }
        }
    }
}