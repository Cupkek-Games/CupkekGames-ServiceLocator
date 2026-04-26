using System;
using System.Text;

namespace CupkekGames.Systems.Editor
{
  /// <summary>
  /// Human-readable <see cref="Type"/> names for the Service Locator debug window (e.g. <c>IAssetCatalog&lt;Sprite&gt;</c> instead of <c>IAssetCatalog`1</c>).
  /// </summary>
  internal static class ServiceLocatorDebugTypeDisplay
  {
    public static string Format(Type type)
    {
      if (type == null)
        return "null";

      if (type.IsArray)
      {
        string brackets = type.GetArrayRank() == 1
          ? "[]"
          : "[" + new string(',', type.GetArrayRank() - 1) + "]";
        return Format(type.GetElementType()) + brackets;
      }

      if (type.IsGenericType)
      {
        var sb = new StringBuilder();
        if (type.DeclaringType != null)
        {
          sb.Append(Format(type.DeclaringType));
          sb.Append('.');
        }

        sb.Append(StripArity(type.Name));
        sb.Append('<');
        Type[] args = type.GetGenericArguments();
        for (int i = 0; i < args.Length; i++)
        {
          if (i > 0)
            sb.Append(", ");
          sb.Append(Format(args[i]));
        }

        sb.Append('>');
        return sb.ToString();
      }

      if (type.DeclaringType != null)
        return Format(type.DeclaringType) + "." + StripArity(type.Name);

      return StripArity(type.Name);
    }

    public static string FormatWithNamespace(Type type)
    {
      if (type == null)
        return "null";
      string ns = type.Namespace;
      string name = Format(type);
      return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    public static bool TypeMatchesSearch(Type type, string filterLower)
    {
      if (string.IsNullOrEmpty(filterLower))
        return true;
      if (type == null)
        return false;

      if (Format(type).ToLowerInvariant().Contains(filterLower))
        return true;
      if (FormatWithNamespace(type).ToLowerInvariant().Contains(filterLower))
        return true;
      if (type.Name.ToLowerInvariant().Contains(filterLower))
        return true;
      if (type.FullName != null && type.FullName.ToLowerInvariant().Contains(filterLower))
        return true;
      if (type.Namespace != null && type.Namespace.ToLowerInvariant().Contains(filterLower))
        return true;

      foreach (Type arg in type.GetGenericArguments())
      {
        if (TypeMatchesSearch(arg, filterLower))
          return true;
      }

      return false;
    }

    private static string StripArity(string name)
    {
      int i = name.IndexOf('`');
      return i < 0 ? name : name.Substring(0, i);
    }
  }
}
