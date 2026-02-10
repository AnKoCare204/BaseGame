using System;

[Serializable]
public class ImpressionData
{
    public AdsMediationType ad_mediation;
    public string ad_type;
    public string ad_source;
    public string ad_unit_name;
    public string ad_format;
    public double ad_revenue;
    public string ad_currency;
    public string placement = string.Empty;

    public ImpressionData() { }

    public ImpressionData(AdsMediationType mediation, string type, string source,
        string unitName, string format, double revenue, string placement = "")
    {
        this.ad_mediation = mediation;
        this.ad_type = type;
        this.ad_source = source;
        this.ad_unit_name = unitName;
        this.ad_format = format;
        this.ad_revenue = revenue;
        this.ad_currency = "USD";
        this.placement = placement;
    }
}