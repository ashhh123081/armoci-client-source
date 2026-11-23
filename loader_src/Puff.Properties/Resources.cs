using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace Puff.Properties;

[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
internal class Resources
{
	private static ResourceManager resourceMan;

	private static CultureInfo resourceCulture;

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static ResourceManager ResourceManager
	{
		get
		{
			if (resourceMan == null)
			{
				ResourceManager resourceManager = new ResourceManager("Puff.Properties.Resources", typeof(Resources).Assembly);
				resourceMan = resourceManager;
			}
			return resourceMan;
		}
	}

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static CultureInfo Culture
	{
		get
		{
			return resourceCulture;
		}
		set
		{
			resourceCulture = value;
		}
	}

	internal static Bitmap _20250529_004353
	{
		get
		{
			object obj = ResourceManager.GetObject("20250529_004353", resourceCulture);
			return (Bitmap)obj;
		}
	}

	internal static Bitmap image_removebg_preview
	{
		get
		{
			object obj = ResourceManager.GetObject("image-removebg-preview", resourceCulture);
			return (Bitmap)obj;
		}
	}

	internal static Bitmap Picsart_25_05_29_00_58_21_895
	{
		get
		{
			object obj = ResourceManager.GetObject("Picsart_25-05-29_00-58-21-895", resourceCulture);
			return (Bitmap)obj;
		}
	}

	internal Resources()
	{
	}
}
