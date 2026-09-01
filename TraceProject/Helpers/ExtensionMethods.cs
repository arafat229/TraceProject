using System.Globalization;

namespace TraceProject.Helpers;

public static class ExtensionMethods
{

    public static string ToTitleCase(this string text)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
    }

    //public static string GetFullErrorMessage(this Exception ex)
    //{
    //    if(ex is AggregateException ex2)
    //    {
    //        return ex2.InnerExceptions.Aggregate("[ ",(string total,Exception next) => total + "[" + next.GetFullErrorMessage() + "] ")+"]";
            
    //    }
    //    string text = ex.Message.Replace(", see inner exception.", "").Trim();
    //    string text2 = ex.InnerException?.GetFullErrorMessage();
    //    if(text2 != null && text2 != text)
    //    {
    //        text = text +"[" + text2 + "]";
    //    }
    //    return text;


    //}
    public static string GetFullErrorMessage(this Exception ex)
    {

        if(ex is AggregateException ex2)
        {
            return ex2.InnerExceptions.Aggregate("[ ",(string currentMesage , Exception next) => currentMesage + "["+ next.GetFullErrorMessage() +"] ")+ " ]"; 

        }

        string text = ex.Message.Replace(", see inner Exception.", "").Trim();

        string text2 = ex.InnerException?.GetFullErrorMessage() ?? "";

        if(text2 != null && text2!= text)
        {
            text = text + "[" + text2 + "]";
        }

        return text;

    }

}

