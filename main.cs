using System;
using System.IO;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Input;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VkImage=Silk.NET.Vulkan.Image;
using VkBuffer=Silk.NET.Vulkan.Buffer;
using VkSemaphore=Silk.NET.Vulkan.Semaphore;
using util;
using System.Numerics;
namespace mainprocess;

unsafe public class MainProcess{
    const int Width=1200;
    const int Height=800;
    static IInputContext input = null!;
    static IWindow window = null!;
    static Vk vk = null!;
    static KhrSurface khrSurface = null!;
    static KhrSwapchain khrSwapchain = null!;
    static Instance instance;
    static SurfaceKHR surface;
    static PhysicalDevice physicalDevice;
    static Device device;
    static Queue graphicsQueue;
    static uint graphicsQueueFamily;
    static SwapchainKHR swapchain;
    static VkImage[] swapchainImages = null!;
    static ImageView[] swapchainImageViews = null!;
    static Format swapchainFormat;
    static Extent2D swapchainExtent;
    static RenderPass renderPass;
    static Framebuffer[] swapchainFramebuffers = null!;
    static PipelineLayout pipelineLayout;
    static Pipeline graphicsPipeline;
    static DescriptorSetLayout descriptorSetLayout;
    static DescriptorPool descriptorPool;
    static DescriptorSet descriptorSet;
    static CommandPool commandPool;
    static CommandBuffer[] commandBuffers = null!;
    static VkSemaphore imageAvailableSemaphore;
    static VkSemaphore renderFinishedSemaphore;
    static Fence inFlightFence;
    static Texture texture = null!;
    public static void Main(){
        CreateWindow();
        CreateVulkan();
        input=window.CreateInput();
        foreach(var mouse in input.Mice){
            Game.mouseEvent(mouse);
        }
        window.Render+=delta=>{
            Game.time+=(float)delta;
            Game.Process(input);
            DrawFrame(Game.time);
        };
        window.Run();
        vk.DeviceWaitIdle(device);
        Cleanup();
    }
    static void CreateWindow(){
        var options=WindowOptions.DefaultVulkan with{Size=new Vector2D<int>(Width, Height),Title="Vulkan"};
        window=Window.Create(options);
        window.Initialize();
        if(window.VkSurface is null){throw new Exception("Vulkan surface is not supported.");}
    }
    static void CreateVulkan(){
        vk=Vk.GetApi();
        CreateInstance();
        CreateSurface();
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateSwapchain();
        CreateImageViews();
        CreateRenderPass();
        CreateDescriptorSetLayout();
        CreateCommandPool();
        texture=new Texture(vk,device,physicalDevice,commandPool,graphicsQueue,"tadokorokouji.png");
        CreateDescriptorPool();
        CreateDescriptorSet();
        CreateGraphicsPipeline();
        CreateFramebuffers();
        CreateCommandBuffers();
        CreateSyncObjects();
        Console.WriteLine("Vulkan initialized.");
    }
    static void CreateInstance(){
        ApplicationInfo appInfo=new(){SType=StructureType.ApplicationInfo,ApiVersion=Vk.Version13};
        byte** requiredExtensions=window.VkSurface!.GetRequiredExtensions(out uint extensionCount);
        InstanceCreateInfo createInfo = new(){
            SType=StructureType.InstanceCreateInfo,
            PApplicationInfo=&appInfo,
            EnabledExtensionCount=extensionCount,
            PpEnabledExtensionNames=requiredExtensions
        };
        Check(vk.CreateInstance(in createInfo,null,out instance),"CreateInstance");
        if(!vk.TryGetInstanceExtension(instance,out khrSurface)){throw new Exception("Could not load VK_KHR_surface.");}
        Console.WriteLine("Vulkan instance created successfully.");
    }
    static void CreateSurface(){
        surface=window.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(),null).ToSurface();
        Console.WriteLine("Surface created.");
    }
    static void PickPhysicalDevice(){
        var devices=vk.GetPhysicalDevices(instance);
        foreach(var candidate in devices){
            uint? queueFamily=FindGraphicsAndPresentQueue(candidate);
            if(queueFamily.HasValue){
                physicalDevice=candidate;
                graphicsQueueFamily=queueFamily.Value;
                break;
            }
        }
        if(physicalDevice.Handle==0){throw new Exception("No suitable Vulkan GPU found.");}
        PhysicalDeviceProperties properties;
        vk.GetPhysicalDeviceProperties(physicalDevice,out properties);
        string gpuName=Silk.NET.Core.Native.SilkMarshal.PtrToString((nint)properties.DeviceName) ?? "(unknown)";
        Console.WriteLine($"Using GPU: {gpuName}");
        Console.WriteLine($"Queue family: {graphicsQueueFamily}");
    }
    static uint? FindGraphicsAndPresentQueue(PhysicalDevice candidate){
        uint queueFamilyCount=0;
        vk.GetPhysicalDeviceQueueFamilyProperties(candidate,ref queueFamilyCount,null);
        var queueFamilies=new QueueFamilyProperties[queueFamilyCount];
        fixed(QueueFamilyProperties* pQueueFamilies=queueFamilies){
            vk.GetPhysicalDeviceQueueFamilyProperties(candidate,ref queueFamilyCount,pQueueFamilies);
        }
        for(uint i=0;i<queueFamilyCount;i++){
            bool graphics=(queueFamilies[(int)i].QueueFlags&QueueFlags.GraphicsBit)!=0;
            if(!graphics){continue;}
            var result=khrSurface.GetPhysicalDeviceSurfaceSupport(candidate,i,surface,out var surfaceSupported);
            Check(result,"GetPhysicalDeviceSurfaceSupport");
            if(surfaceSupported){return i;}
        }
        return null;
    }
    static void CreateLogicalDevice(){
        float queuePriority=1.0f;
        DeviceQueueCreateInfo queueCreateInfo = new(){
            SType=StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex=graphicsQueueFamily,
            QueueCount=1,
            PQueuePriorities=&queuePriority
        };
        PhysicalDeviceFeatures features=new();
        string[] extensions={KhrSwapchain.ExtensionName};
        byte** extensionNames=(byte**)Silk.NET.Core.Native.SilkMarshal.StringArrayToPtr(extensions);
        DeviceCreateInfo createInfo=new(){
            SType=StructureType.DeviceCreateInfo,
            QueueCreateInfoCount=1,
            PQueueCreateInfos=&queueCreateInfo,
            PEnabledFeatures=&features,
            EnabledExtensionCount=(uint)extensions.Length,
            PpEnabledExtensionNames=extensionNames
        };
        Check(vk.CreateDevice(physicalDevice,in createInfo,null,out device),"CreateDevice");
        Silk.NET.Core.Native.SilkMarshal.Free((nint)extensionNames);
        vk.GetDeviceQueue(device,graphicsQueueFamily,0,out graphicsQueue);
        if(!vk.TryGetDeviceExtension(instance,device,out khrSwapchain)){throw new Exception("Could not load VK_KHR_swapchain.");}
        Console.WriteLine("Logical device created.");
    }
    static void CreateSwapchain(){
        Check(khrSurface.GetPhysicalDeviceSurfaceCapabilities(physicalDevice,surface,out var capabilities),"GetPhysicalDeviceSurfaceCapabilities");
        uint formatCount = 0;
        Check(khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice,surface,ref formatCount,null),"GetPhysicalDeviceSurfaceFormats");
        var formats=new SurfaceFormatKHR[formatCount];
        fixed(SurfaceFormatKHR* pFormats=formats){
            Check(khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice,surface,ref formatCount,pFormats),"GetPhysicalDeviceSurfaceFormats");
        }
        if(formats.Length==0){throw new Exception("No surface formats available.");}
        SurfaceFormatKHR chosenFormat=formats[0];
        foreach (var format in formats){
            if(format.Format==Format.B8G8R8A8Srgb&&format.ColorSpace==ColorSpaceKHR.SpaceSrgbNonlinearKhr){
                chosenFormat=format;
                break;
            }
        }
        swapchainFormat=chosenFormat.Format;
        uint presentModeCount=0;
        Check(khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice,surface,ref presentModeCount,null),"GetPhysicalDeviceSurfacePresentModes");
        var presentModes=new PresentModeKHR[presentModeCount];
        fixed(PresentModeKHR* pPresentModes=presentModes){
            Check(khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice,surface,ref presentModeCount,pPresentModes),"GetPhysicalDeviceSurfacePresentModes");
        }
        PresentModeKHR presentMode=PresentModeKHR.FifoKhr;
        foreach(var mode in presentModes){if(mode==PresentModeKHR.MailboxKhr){presentMode=mode;break;}}
        swapchainExtent=capabilities.CurrentExtent;
        if(swapchainExtent.Width==uint.MaxValue){
            var size=window.FramebufferSize;
            swapchainExtent=new Extent2D{
                Width=(uint)Math.Clamp(size.X,(int)capabilities.MinImageExtent.Width,(int)capabilities.MaxImageExtent.Width),
                Height=(uint)Math.Clamp(size.Y,(int)capabilities.MinImageExtent.Height,(int)capabilities.MaxImageExtent.Height)
            };
        }
        uint imageCount=capabilities.MinImageCount+1;
        if(capabilities.MaxImageCount!=0&&imageCount>capabilities.MaxImageCount){
            imageCount=capabilities.MaxImageCount;
        }
        SwapchainCreateInfoKHR createInfo=new(){
            SType=StructureType.SwapchainCreateInfoKhr,
            Surface=surface,
            MinImageCount=imageCount,
            ImageFormat=chosenFormat.Format,
            ImageColorSpace=chosenFormat.ColorSpace,
            ImageExtent=swapchainExtent,
            ImageArrayLayers=1,
            ImageUsage=ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode=SharingMode.Exclusive,
            PreTransform=capabilities.CurrentTransform,
            CompositeAlpha=CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode=presentMode,
            Clipped=true,
            OldSwapchain=default
        };
        Check(khrSwapchain.CreateSwapchain(device,in createInfo,null,out swapchain),"CreateSwapchain");
        uint actualImageCount = 0;
        Check(khrSwapchain.GetSwapchainImages(device,swapchain,ref actualImageCount,null),"GetSwapchainImages");
        swapchainImages=new VkImage[actualImageCount];
        fixed(VkImage* pImages=swapchainImages){
            Check(khrSwapchain.GetSwapchainImages(device,swapchain,ref actualImageCount,pImages),"GetSwapchainImages");
        }
        Console.WriteLine($"Swapchain: {swapchainExtent.Width}x{swapchainExtent.Height}, {swapchainImages.Length} images");
    }
    static void CreateImageViews(){
        swapchainImageViews=new ImageView[swapchainImages.Length];
        for(int i=0;i<swapchainImages.Length;i++){
            ImageViewCreateInfo createInfo=new(){
                SType=StructureType.ImageViewCreateInfo,
                Image=swapchainImages[i],
                ViewType=ImageViewType.Type2D,
                Format=swapchainFormat,
                Components={R=ComponentSwizzle.Identity,G=ComponentSwizzle.Identity,B=ComponentSwizzle.Identity,A=ComponentSwizzle.Identity},
                SubresourceRange={
                    AspectMask=ImageAspectFlags.ColorBit,
                    BaseMipLevel=0,
                    LevelCount=1,
                    BaseArrayLayer=0,
                    LayerCount=1
                }
            };
            Check(vk.CreateImageView(device,in createInfo,null,out swapchainImageViews[i]),"CreateImageView");
        }
    }
    static void CreateRenderPass(){
        AttachmentDescription colorAttachment=new(){
            Format=swapchainFormat,
            Samples=SampleCountFlags.Count1Bit,
            LoadOp=AttachmentLoadOp.Clear,
            StoreOp=AttachmentStoreOp.Store,
            StencilLoadOp=AttachmentLoadOp.DontCare,
            StencilStoreOp=AttachmentStoreOp.DontCare,
            InitialLayout=ImageLayout.Undefined,
            FinalLayout=ImageLayout.PresentSrcKhr
        };
        AttachmentReference colorAttachmentRef=new(){Attachment=0,Layout=ImageLayout.ColorAttachmentOptimal};
        SubpassDescription subpass=new(){
            PipelineBindPoint=PipelineBindPoint.Graphics,
            ColorAttachmentCount=1,
            PColorAttachments=&colorAttachmentRef
        };
        SubpassDependency dependency=new(){
            SrcSubpass=Vk.SubpassExternal,
            DstSubpass=0,
            SrcStageMask=PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask=0,
            DstStageMask=PipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask=AccessFlags.ColorAttachmentWriteBit
        };
        RenderPassCreateInfo createInfo=new(){
            SType=StructureType.RenderPassCreateInfo,
            AttachmentCount=1,
            PAttachments=&colorAttachment,
            SubpassCount=1,
            PSubpasses=&subpass,
            DependencyCount=1,
            PDependencies=&dependency
        };
        Check(vk.CreateRenderPass(device,in createInfo,null,out renderPass),"CreateRenderPass");
    }
    static void CreateDescriptorSetLayout(){
        DescriptorSetLayoutBinding binding=new(){
            Binding=0,
            DescriptorCount=1,
            DescriptorType=DescriptorType.CombinedImageSampler,
            StageFlags=ShaderStageFlags.FragmentBit
        };
        DescriptorSetLayoutCreateInfo createInfo=new(){
            SType=StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount=1,
            PBindings=&binding
        };
        Check(vk.CreateDescriptorSetLayout(device,in createInfo,null,out descriptorSetLayout),"CreateDescriptorSetLayout");
    }
    static void CreateDescriptorPool(){
        DescriptorPoolSize poolSize=new(){
            Type=DescriptorType.CombinedImageSampler,
            DescriptorCount=1
        };
        DescriptorPoolCreateInfo createInfo=new(){
            SType=StructureType.DescriptorPoolCreateInfo,
            MaxSets=1,
            PoolSizeCount=1,
            PPoolSizes=&poolSize
        };
        Check(vk.CreateDescriptorPool(device,in createInfo,null,out descriptorPool),"CreateDescriptorPool");
    }
    static void CreateDescriptorSet(){
        var layouts=new[]{descriptorSetLayout};
        DescriptorSetAllocateInfo allocateInfo=new(){
            SType=StructureType.DescriptorSetAllocateInfo,
            DescriptorPool=descriptorPool,
            DescriptorSetCount=1
        };
        fixed(DescriptorSetLayout* pLayouts=layouts){
            allocateInfo.PSetLayouts=pLayouts;
            Check(vk.AllocateDescriptorSets(device,in allocateInfo,out descriptorSet),"AllocateDescriptorSet");
        }
        DescriptorImageInfo imageInfo=new(){
            Sampler=texture.Sampler,
            ImageView=texture.ImageView,
            ImageLayout=ImageLayout.ShaderReadOnlyOptimal
        };
        WriteDescriptorSet write=new(){
            SType=StructureType.WriteDescriptorSet,
            DstSet=descriptorSet,
            DstBinding=0,
            DstArrayElement=0,
            DescriptorCount=1,
            DescriptorType=DescriptorType.CombinedImageSampler,
            PImageInfo=&imageInfo
        };
        vk.UpdateDescriptorSets(device,1,in write,0,null);
    }
    static void CreateGraphicsPipeline(){
        byte[] vertCode=File.ReadAllBytes("shaders/shader.vert.spv");
        byte[] fragCode=File.ReadAllBytes("shaders/shader.frag.spv");
        //フラグメントコンパイル時、自動でライブラリが読み込まれる。
        //すなわち、f.glslの変更は、フラグメントシェーダの再コンパイルを意味する。
        ShaderModule vertShaderModule=CreateShaderModule(vertCode);
        ShaderModule fragShaderModule=CreateShaderModule(fragCode);
        nint vertName=Silk.NET.Core.Native.SilkMarshal.StringToPtr("main");
        nint fragName=Silk.NET.Core.Native.SilkMarshal.StringToPtr("main");
        PipelineShaderStageCreateInfo vertStage=new(){
            SType=StructureType.PipelineShaderStageCreateInfo,
            Stage=ShaderStageFlags.VertexBit,
            Module=vertShaderModule,
            PName=(byte*)vertName
        };
        PipelineShaderStageCreateInfo fragStage=new(){
            SType=StructureType.PipelineShaderStageCreateInfo,
            Stage=ShaderStageFlags.FragmentBit,
            Module=fragShaderModule,
            PName=(byte*)fragName
        };
        var stages=stackalloc PipelineShaderStageCreateInfo[]{
            vertStage,
            fragStage
        };
        PipelineVertexInputStateCreateInfo vertexInput=new(){
            SType=StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount=0,
            VertexAttributeDescriptionCount=0
        };
        PipelineInputAssemblyStateCreateInfo inputAssembly=new(){
            SType=StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology=PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable=false
        };
        Viewport viewport=new(){X=0,Y=0,Width=swapchainExtent.Width,Height=swapchainExtent.Height,MinDepth=0,MaxDepth=1};
        Rect2D scissor=new(){Offset={X=0,Y=0},Extent=swapchainExtent};
        PipelineViewportStateCreateInfo viewportState=new(){SType=StructureType.PipelineViewportStateCreateInfo,ViewportCount=1,PViewports=&viewport,ScissorCount=1,PScissors=&scissor};
        PipelineRasterizationStateCreateInfo rasterizer=new(){
            SType=StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable=false,
            RasterizerDiscardEnable=false,
            PolygonMode=PolygonMode.Fill,
            LineWidth=1.0f,
            CullMode=CullModeFlags.None,
            FrontFace=FrontFace.CounterClockwise,
            DepthBiasEnable=false
        };
        PipelineMultisampleStateCreateInfo multisampling=new(){
            SType=StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = false,
            RasterizationSamples=SampleCountFlags.Count1Bit
        };
        PipelineColorBlendAttachmentState colorBlendAttachment=new(){
            BlendEnable=false,
            ColorWriteMask=ColorComponentFlags.RBit|ColorComponentFlags.GBit|ColorComponentFlags.BBit|ColorComponentFlags.ABit
        };
        PipelineColorBlendStateCreateInfo colorBlending=new(){
            SType=StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable=false,
            AttachmentCount=1,
            PAttachments=&colorBlendAttachment
        };
        var layouts=new[]{descriptorSetLayout};
        //uni
        PushConstantRange pushConstantRange=new(){StageFlags=ShaderStageFlags.FragmentBit,Offset=0,Size=Game.unibufferSize};
        PipelineLayoutCreateInfo layoutInfo=new(){
            SType=StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount=1,
            PushConstantRangeCount=1,
            PPushConstantRanges=&pushConstantRange
        };
        fixed(DescriptorSetLayout* pLayouts=layouts){
            layoutInfo.PSetLayouts=pLayouts;
            Check(vk.CreatePipelineLayout(device,in layoutInfo,null,out pipelineLayout),"CreatePipelineLayout");
        }
        //パイプライン
        GraphicsPipelineCreateInfo pipelineInfo=new(){
            SType=StructureType.GraphicsPipelineCreateInfo,
            StageCount=2,
            PStages=stages,
            PVertexInputState=&vertexInput,
            PInputAssemblyState=&inputAssembly,
            PViewportState=&viewportState,
            PRasterizationState=&rasterizer,
            PMultisampleState=&multisampling,
            PColorBlendState=&colorBlending,
            Layout=pipelineLayout,
            RenderPass=renderPass,
            Subpass=0
        };
        Check(vk.CreateGraphicsPipelines(device,default,1,in pipelineInfo,null,out graphicsPipeline),"CreateGraphicsPipelines");
        vk.DestroyShaderModule(device,vertShaderModule,null);
        vk.DestroyShaderModule(device,fragShaderModule,null);
        Silk.NET.Core.Native.SilkMarshal.Free(vertName);
        Silk.NET.Core.Native.SilkMarshal.Free(fragName);
        Console.WriteLine("Graphics pipeline created.");
    }
    static ShaderModule CreateShaderModule(byte[] code){
        fixed (byte* pCode=code){
            ShaderModuleCreateInfo createInfo=new(){
                SType=StructureType.ShaderModuleCreateInfo,
                CodeSize=(nuint)code.Length,
                PCode=(uint*)pCode
            };
            Check(vk.CreateShaderModule(device,in createInfo,null,out ShaderModule shaderModule),"CreateShaderModule");
            return shaderModule;
        }
    }
    static void CreateFramebuffers(){
        swapchainFramebuffers=new Framebuffer[swapchainImageViews.Length];
        for(int i=0;i<swapchainImageViews.Length;i++){
            var attachment=swapchainImageViews[i];
            FramebufferCreateInfo createInfo=new(){
                SType=StructureType.FramebufferCreateInfo,
                RenderPass=renderPass,
                AttachmentCount=1,
                PAttachments=&attachment,
                Width=swapchainExtent.Width,
                Height=swapchainExtent.Height,
                Layers=1
            };
            Check(vk.CreateFramebuffer(device,in createInfo,null,out swapchainFramebuffers[i]),"CreateFramebuffer");
        }
    }
    static void CreateCommandPool(){
        CommandPoolCreateInfo createInfo=new(){
            SType=StructureType.CommandPoolCreateInfo,
            Flags=CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex=graphicsQueueFamily
        };
        Check(vk.CreateCommandPool(device,in createInfo,null,out commandPool),"CreateCommandPool");
    }
    static void CreateCommandBuffers(){
        commandBuffers=new CommandBuffer[swapchainFramebuffers.Length];
        CommandBufferAllocateInfo allocateInfo=new(){
            SType=StructureType.CommandBufferAllocateInfo,
            CommandPool=commandPool,
            Level=CommandBufferLevel.Primary,
            CommandBufferCount=(uint)commandBuffers.Length
        };
        fixed(CommandBuffer* pCommandBuffers=commandBuffers){
            Check(vk.AllocateCommandBuffers(device,in allocateInfo,pCommandBuffers),"AllocateCommandBuffers");
        }
        for(int i=0;i<commandBuffers.Length;i++){
            RecordCommandBuffer(commandBuffers[i],swapchainFramebuffers[i]);
        }
    }
    static void RecordCommandBuffer(CommandBuffer commandBuffer,Framebuffer framebuffer){
        CommandBufferBeginInfo beginInfo=new(){
            SType=StructureType.CommandBufferBeginInfo
        };
        Check(vk.BeginCommandBuffer(commandBuffer,in beginInfo),"BeginCommandBuffer");
        ClearValue clearColor=new(){Color={Float32_0=0.0f,Float32_1=0.0f,Float32_2=0.0f,Float32_3=1.0f}};
        RenderPassBeginInfo renderPassBeginInfo=new(){
            SType=StructureType.RenderPassBeginInfo,
            RenderPass=renderPass,
            Framebuffer=framebuffer,
            RenderArea={Offset={X=0,Y=0},Extent=swapchainExtent},
            ClearValueCount=1,
            PClearValues=&clearColor
        };
        Uniforms uniforms=Game.getCurrent(swapchainExtent);
        vk.CmdPushConstants(commandBuffer,pipelineLayout,ShaderStageFlags.FragmentBit,0,Game.unibufferSize,&uniforms);
        vk.CmdBeginRenderPass(commandBuffer,in renderPassBeginInfo,SubpassContents.Inline);
        vk.CmdBindPipeline(commandBuffer,PipelineBindPoint.Graphics,graphicsPipeline);
        vk.CmdBindDescriptorSets(commandBuffer,PipelineBindPoint.Graphics,pipelineLayout,0,1,in descriptorSet,0,null);
        // Fullscreen triangle
        vk.CmdDraw(commandBuffer,3,1,0,0);
        vk.CmdEndRenderPass(commandBuffer);
        Check(vk.EndCommandBuffer(commandBuffer),"EndCommandBuffer");
    }
    static void CreateSyncObjects(){
        SemaphoreCreateInfo semaphoreInfo=new(){SType=StructureType.SemaphoreCreateInfo};
        FenceCreateInfo fenceInfo=new(){
            SType=StructureType.FenceCreateInfo,
            Flags=FenceCreateFlags.SignaledBit
        };
        Check(vk.CreateSemaphore(device,in semaphoreInfo,null,out imageAvailableSemaphore),"CreateSemaphore");
        Check(vk.CreateSemaphore(device,in semaphoreInfo,null,out renderFinishedSemaphore),"CreateSemaphore");
        Check(vk.CreateFence(device,in fenceInfo,null,out inFlightFence),"CreateFence");
    }
    static void DrawFrame(double deltaTime){
        Check(vk.WaitForFences(device,1,in inFlightFence,true,ulong.MaxValue),"WaitForFences");
        uint imageIndex=0;
        Check(khrSwapchain.AcquireNextImage(device,swapchain,ulong.MaxValue,imageAvailableSemaphore,default,ref imageIndex),"AcquireNextImage");
        CommandBuffer commandBuffer=commandBuffers[imageIndex];
        RecordCommandBuffer(commandBuffer,swapchainFramebuffers[imageIndex]);
        Check(vk.ResetFences(device,1,in inFlightFence),"ResetFences");
        var waitSemaphores=stackalloc VkSemaphore[]{imageAvailableSemaphore};
        var waitStages=stackalloc PipelineStageFlags[]{PipelineStageFlags.ColorAttachmentOutputBit};
        var signalSemaphores=stackalloc VkSemaphore[]{renderFinishedSemaphore};
        SubmitInfo submitInfo=new(){
            SType=StructureType.SubmitInfo,
            WaitSemaphoreCount=1,
            PWaitSemaphores=waitSemaphores,
            PWaitDstStageMask=waitStages,
            CommandBufferCount=1,
            PCommandBuffers=&commandBuffer,
            SignalSemaphoreCount=1,
            PSignalSemaphores=signalSemaphores
        };
        Check(vk.QueueSubmit(graphicsQueue,1,in submitInfo,inFlightFence),"QueueSubmit");
        var swapchains=stackalloc SwapchainKHR[]{swapchain};
        PresentInfoKHR presentInfo=new(){
            SType=StructureType.PresentInfoKhr,
            WaitSemaphoreCount=1,
            PWaitSemaphores=signalSemaphores,
            SwapchainCount=1,
            PSwapchains=swapchains,
            PImageIndices=&imageIndex
        };
        Check(khrSwapchain.QueuePresent(graphicsQueue,in presentInfo),"QueuePresent");
    }
    static void Cleanup(){
        texture.Dispose();
        vk.DestroyDescriptorPool(device,descriptorPool,null);
        vk.DestroyDescriptorSetLayout(device,descriptorSetLayout,null);
        vk.DestroyPipeline(device,graphicsPipeline,null);
        vk.DestroyPipelineLayout(device,pipelineLayout,null);
        foreach (var framebuffer in swapchainFramebuffers){
            vk.DestroyFramebuffer(device,framebuffer,null);
        }
        vk.DestroyRenderPass(device,renderPass,null);
        foreach (var imageView in swapchainImageViews){
            vk.DestroyImageView(device,imageView,null);
        }
        khrSwapchain.DestroySwapchain(device,swapchain,null);
        vk.DestroyFence(device,inFlightFence,null);
        vk.DestroySemaphore(device,renderFinishedSemaphore,null);
        vk.DestroySemaphore(device,imageAvailableSemaphore,null);
        vk.DestroyCommandPool(device,commandPool,null);
        vk.DestroyDevice(device,null);
        khrSurface.DestroySurface(instance,surface,null);
        vk.DestroyInstance(instance,null);
        vk.Dispose();
        input.Dispose();
        window.Dispose();
    }
    static void Check(Result result,string operation){
        if(result!=Result.Success){throw new Exception($"{operation} failed: {result}");}
    }
    unsafe class Texture:IDisposable{
        private readonly Vk vk;
        private readonly Device device;
        private readonly PhysicalDevice physicalDevice;
        private readonly CommandPool commandPool;
        private readonly Queue graphicsQueue;
        public VkImage Image{get;private set;}
        public ImageView ImageView{get;private set;}
        public Sampler Sampler{get;private set;}
        private DeviceMemory imageMemory;
        public uint Width{get;}
        public uint Height{get;}
        public Texture(Vk vk,Device device,PhysicalDevice physicalDevice,CommandPool commandPool,Queue graphicsQueue,string path){
            this.vk=vk;
            this.device=device;
            this.physicalDevice=physicalDevice;
            this.commandPool=commandPool;
            this.graphicsQueue=graphicsQueue;
            using var bitmap=SixLabors.ImageSharp.Image.Load<Rgba32>(path);
            Width=(uint)bitmap.Width;
            Height=(uint)bitmap.Height;
            int pixelCount=checked(bitmap.Width*bitmap.Height*4);
            byte[] pixels=new byte[pixelCount];
            bitmap.CopyPixelDataTo(pixels);
            CreateImage();
            UploadPixels(pixels);
            CreateImageView();
            CreateSampler();
            Console.WriteLine($"Texture loaded: {path} ({Width}x{Height})");
        }
        private void CreateImage(){
            ImageCreateInfo imageInfo=new(){
                SType=StructureType.ImageCreateInfo,
                ImageType=ImageType.Type2D,
                Extent=new Extent3D{Width=Width,Height=Height,Depth=1},
                MipLevels=1,
                ArrayLayers=1,
                Format=Format.R8G8B8A8Unorm,
                Tiling=ImageTiling.Optimal,
                InitialLayout=ImageLayout.Undefined,
                Usage=ImageUsageFlags.TransferDstBit|ImageUsageFlags.SampledBit,
                SharingMode=SharingMode.Exclusive,
                Samples=SampleCountFlags.Count1Bit
            };
            VkImage image;
            Check(vk.CreateImage(device,in imageInfo,null,out image),"CreateImage");
            Image=image;
            MemoryRequirements requirements;
            vk.GetImageMemoryRequirements(device,Image,out requirements);
            uint memoryType=FindMemoryType(requirements.MemoryTypeBits,MemoryPropertyFlags.DeviceLocalBit);
            MemoryAllocateInfo allocateInfo=new(){SType=StructureType.MemoryAllocateInfo,AllocationSize=requirements.Size,MemoryTypeIndex=memoryType};
            Check(vk.AllocateMemory(device,in allocateInfo,null,out imageMemory),"AllocateImageMemory");
            Check(vk.BindImageMemory(device,Image,imageMemory,0),"BindImageMemory");
        }
        private void UploadPixels(byte[] pixels){
            ulong imageSize=(ulong)pixels.Length;
            VkBuffer stagingBuffer;
            DeviceMemory stagingMemory;
            BufferCreateInfo bufferInfo=new(){
                SType=StructureType.BufferCreateInfo,
                Size=imageSize,
                Usage=BufferUsageFlags.TransferSrcBit,
                SharingMode=SharingMode.Exclusive
            };
            Check(vk.CreateBuffer(device,in bufferInfo,null,out stagingBuffer),"CreateStagingBuffer");
            MemoryRequirements requirements;
            vk.GetBufferMemoryRequirements(device,stagingBuffer,out requirements);
            uint memoryType=FindMemoryType(requirements.MemoryTypeBits,MemoryPropertyFlags.HostVisibleBit|MemoryPropertyFlags.HostCoherentBit);
            MemoryAllocateInfo allocateInfo=new(){SType=StructureType.MemoryAllocateInfo,AllocationSize=requirements.Size,MemoryTypeIndex=memoryType};
            Check(vk.AllocateMemory(device,in allocateInfo,null,out stagingMemory),"AllocateStagingMemory");
            Check(vk.BindBufferMemory(device,stagingBuffer,stagingMemory,0),"BindStagingMemory");
            void* mapped=null;
            Check(vk.MapMemory(device,stagingMemory,0,imageSize,0,ref mapped),"MapStagingMemory");
            fixed(byte* pPixels=pixels){
                System.Buffer.MemoryCopy(pPixels,mapped,pixels.Length,pixels.Length);
            }
            vk.UnmapMemory(device,stagingMemory);
            TransitionImageLayout(ImageLayout.Undefined,ImageLayout.TransferDstOptimal);
            CopyBufferToImage(stagingBuffer,Image);
            TransitionImageLayout(ImageLayout.TransferDstOptimal,ImageLayout.ShaderReadOnlyOptimal);
            vk.DestroyBuffer(device,stagingBuffer,null);
            vk.FreeMemory(device,stagingMemory,null);
        }
        private void TransitionImageLayout(ImageLayout oldLayout,ImageLayout newLayout){
            CommandBuffer commandBuffer=BeginSingleTimeCommands();
            ImageMemoryBarrier barrier=new(){
                SType=StructureType.ImageMemoryBarrier,
                OldLayout=oldLayout,
                NewLayout=newLayout,
                SrcQueueFamilyIndex=Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex=Vk.QueueFamilyIgnored,
                Image=Image,
                SubresourceRange={
                    AspectMask=ImageAspectFlags.ColorBit,
                    BaseMipLevel=0,
                    LevelCount=1,
                    BaseArrayLayer=0,
                    LayerCount=1
                }
            };
            PipelineStageFlags sourceStage;
            PipelineStageFlags destinationStage;
            if(oldLayout==ImageLayout.Undefined && newLayout==ImageLayout.TransferDstOptimal){
                barrier.SrcAccessMask=0;
                barrier.DstAccessMask=AccessFlags.TransferWriteBit;
                sourceStage=PipelineStageFlags.TopOfPipeBit;
                destinationStage=PipelineStageFlags.TransferBit;
            }else if(oldLayout==ImageLayout.TransferDstOptimal && newLayout==ImageLayout.ShaderReadOnlyOptimal){
                barrier.SrcAccessMask=AccessFlags.TransferWriteBit;
                barrier.DstAccessMask=AccessFlags.ShaderReadBit;
                sourceStage=PipelineStageFlags.TransferBit;
                destinationStage=PipelineStageFlags.FragmentShaderBit;
            }else{throw new Exception($"Unsupported image transition: {oldLayout} -> {newLayout}");}
            vk.CmdPipelineBarrier(commandBuffer,sourceStage,destinationStage,0,0,null,0,null,1,in barrier);
            EndSingleTimeCommands(commandBuffer);
        }
        private void CopyBufferToImage(VkBuffer buffer,VkImage image){
            CommandBuffer commandBuffer=BeginSingleTimeCommands();
            BufferImageCopy region=new(){
                BufferOffset=0,
                BufferRowLength=0,
                BufferImageHeight=0,
                ImageSubresource={
                    AspectMask=ImageAspectFlags.ColorBit,
                    MipLevel=0,
                    BaseArrayLayer=0,
                    LayerCount=1
                },
                ImageOffset=new Offset3D{X=0,Y=0,Z=0},
                ImageExtent=new Extent3D{Width=Width,Height=Height,Depth=1}
            };
            vk.CmdCopyBufferToImage(commandBuffer,buffer,image,ImageLayout.TransferDstOptimal,1,in region);
            EndSingleTimeCommands(commandBuffer);
        }
        private CommandBuffer BeginSingleTimeCommands(){
            CommandBufferAllocateInfo allocateInfo=new(){
                SType=StructureType.CommandBufferAllocateInfo,
                Level=CommandBufferLevel.Primary,
                CommandPool=commandPool,
                CommandBufferCount=1
            };
            CommandBuffer commandBuffer;
            Check(vk.AllocateCommandBuffers(device,in allocateInfo,out commandBuffer),"AllocateSingleTimeCommandBuffer");
            CommandBufferBeginInfo beginInfo=new(){
                SType=StructureType.CommandBufferBeginInfo,
                Flags=CommandBufferUsageFlags.OneTimeSubmitBit
            };
            Check(vk.BeginCommandBuffer(commandBuffer,in beginInfo),"BeginSingleTimeCommandBuffer");
            return commandBuffer;
        }
        private void EndSingleTimeCommands(CommandBuffer commandBuffer){
            Check(vk.EndCommandBuffer(commandBuffer),"EndSingleTimeCommandBuffer");
            SubmitInfo submitInfo=new(){SType=StructureType.SubmitInfo,CommandBufferCount=1,PCommandBuffers=&commandBuffer};
            Check(vk.QueueSubmit(graphicsQueue,1,in submitInfo,default),"SubmitSingleTimeCommandBuffer");
            Check(vk.QueueWaitIdle(graphicsQueue),"QueueWaitIdle");
            vk.FreeCommandBuffers(device,commandPool,1,in commandBuffer);
        }
        private void CreateImageView(){
            ImageViewCreateInfo createInfo=new(){
                SType=StructureType.ImageViewCreateInfo,
                Image=Image,
                ViewType=ImageViewType.Type2D,
                Format=Format.R8G8B8A8Unorm,
                SubresourceRange={
                    AspectMask=ImageAspectFlags.ColorBit,
                    BaseMipLevel=0,
                    LevelCount=1,
                    BaseArrayLayer=0,
                    LayerCount=1
                }
            };
            ImageView imageView;
            Check(vk.CreateImageView(device,in createInfo,null,out imageView),"CreateTextureImageView");
            ImageView=imageView;
        }
        private void CreateSampler(){
            SamplerCreateInfo createInfo=new(){
                SType=StructureType.SamplerCreateInfo,
                MagFilter=Filter.Linear,
                MinFilter=Filter.Linear,
                AddressModeU=SamplerAddressMode.ClampToEdge,
                AddressModeV=SamplerAddressMode.ClampToEdge,
                AddressModeW=SamplerAddressMode.ClampToEdge,
                AnisotropyEnable=false,
                BorderColor=BorderColor.IntOpaqueBlack,
                UnnormalizedCoordinates=false,
                CompareEnable=false,
                MipmapMode=SamplerMipmapMode.Linear,
                MinLod=0,
                MaxLod=0,
                MipLodBias=0
            };
            Sampler sampler;
            Check(vk.CreateSampler(device,in createInfo,null,out sampler),"CreateTextureSampler");
            Sampler=sampler;
        }
        private uint FindMemoryType(uint typeFilter,MemoryPropertyFlags properties){
            PhysicalDeviceMemoryProperties memoryProperties;
            vk.GetPhysicalDeviceMemoryProperties(physicalDevice,out memoryProperties);
            for(uint i=0;i<memoryProperties.MemoryTypeCount;i++){
                if((typeFilter&(1u << (int)i))!=0 && (memoryProperties.MemoryTypes[(int)i].PropertyFlags&properties)==properties){
                    return i;
                }
            }
            throw new Exception("Suitable Vulkan memory type not found.");
        }
        public void Dispose(){
            if(Sampler.Handle != 0){
                vk.DestroySampler(device,Sampler,null);
            }
            if(ImageView.Handle != 0){
                vk.DestroyImageView(device,ImageView,null);
            }
            if (Image.Handle != 0){
                vk.DestroyImage(device,Image,null);
            }
            if (imageMemory.Handle != 0){
                vk.FreeMemory(device,imageMemory,null);
            }
        }
    }
}