using System;
using System.Linq;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Azure.Management.CosmosDB.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Network.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;

namespace AzureVM
{
   class Program
   {
      static void Main(string[] args)
      {
         var credentials = SdkContext.AzureCredentialsFactory
                                     .FromFile("./azureauth.properties");

         Console.WriteLine("Creating VM on Azure subscription: " + credentials.DefaultSubscriptionId);

         var azure = Azure.Configure()
                          .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                          .Authenticate(credentials)
                          .WithDefaultSubscription();

         var vms = azure.VirtualMachines.List().ToList();
         if (vms.Count > 0)
         {
            Console.WriteLine("Current VM count: " + vms.Count);
         }

         foreach (var vm in vms)
         {
            Console.WriteLine("VM: {0}, Size: {1}", vm.Name, vm.Size.Value);
         }

         Console.WriteLine("Press any key to create a new VM (Press Q to quit)...");

         if (Console.ReadKey().Key == ConsoleKey.Q)
         {
            return;
         }

         //Resource Group
         var location = Region.EuropeNorth;
         var resourceGrp = "CHYA_APP_VM_GRP";
         Console.WriteLine($"Creating resource group {resourceGrp} ..."); 
         var resourceGroup = azure.ResourceGroups.Define(resourceGrp).WithRegion(location).Create();

         //Virtual Network
         var virtualNetwork = "chya_app_vm_vn";
         var virtualNetworkAddress = "10.0.2.0/24";
         var subnetName = "default";
         var subnetAddress = "10.0.2.0/24";
         Console.WriteLine($"Creating virtual network {virtualNetwork} ...");
         var network = azure.Networks.Define(virtualNetwork)
                            .WithRegion(location)
                            .WithExistingResourceGroup(resourceGrp)
                            .WithAddressSpace(virtualNetworkAddress)
                            .WithSubnet(subnetName, subnetAddress)
                            .Create();

         //Public IP
         var publicIp = "chya_app_vm_pip";
         Console.WriteLine($"Creating public IP {publicIp} ...");
         var publicIP = azure.PublicIPAddresses.Define(publicIp)
                             .WithRegion(location)
                             .WithExistingResourceGroup(resourceGrp)
                             .Create();

         //Security Group
         var securityGrp = "chya_app_vm_sg";
         Console.WriteLine($"Creating Network Security Group {securityGrp} ...");
         var nsg = azure.NetworkSecurityGroups.Define(securityGrp)
                        .WithRegion(location)
                        .WithExistingResourceGroup(resourceGrp)
                        .Create();

         //You need a security rule for RDP
         Console.WriteLine($"Creating a Security Rule for allowing the remote access");
         nsg.Update()
            .DefineRule("Allow-RDP")
            .AllowInbound()
            .FromAnyAddress()
            .FromAnyPort()
            .ToAnyAddress()
            .ToPort(3389)
            .WithProtocol(SecurityRuleProtocol.Tcp)
            .WithPriority(100)
            .WithDescription("Allow-RDP")
            .Attach()
            .Apply();

         //Network interface card
         var networkInterfaceCard = "chya_app_vm_nic";
         Console.WriteLine($"Creating network interface {networkInterfaceCard} ...");
         var nic = azure.NetworkInterfaces.Define(networkInterfaceCard)
                        .WithRegion(location)
                        .WithExistingResourceGroup(resourceGrp)
                        .WithExistingPrimaryNetwork(network)
                        .WithSubnet(subnetName)
                        .WithPrimaryPrivateIPAddressDynamic()
                        .WithExistingNetworkSecurityGroup(nsg)
                        .WithExistingPrimaryPublicIPAddress(publicIP)
                        .Create();

         //Virtual machine
         var vmName = "CHYA-APP-VM";
         var adminUser = "chyatest";
         var adminPassword = "Yangchengkai830513";
         Console.WriteLine($"Creating virtual machine {vmName} ...");
         azure.VirtualMachines.Define(vmName)
              .WithRegion(location)
              .WithExistingResourceGroup(resourceGrp)
              .WithExistingPrimaryNetworkInterface(nic)
              .WithLatestWindowsImage("MicrosoftWindowsServer", "WindowsServer",
                                      "2019-Datacenter")
              .WithAdminUsername(adminUser)
              .WithAdminPassword(adminPassword)
              .WithComputerName(vmName)
              .WithSize(VirtualMachineSizeTypes.StandardB1ms)
              .Create();

         Console.WriteLine("Done! Press any key to quit...");
         Console.ReadKey();
      }
   }
}
