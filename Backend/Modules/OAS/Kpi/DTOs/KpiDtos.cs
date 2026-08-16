namespace MyApi.Modules.OAS.Kpi.DTOs;

public class OasKpiDailyDto
{
    public int OpeningMin { get; set; }
    public double Availability { get; set; }
    public double Quality { get; set; }
    public double? Performance { get; set; }
    public bool CadenceKnown { get; set; } = true;
    public double? Oee { get; set; }
    public int StopMinutes { get; set; }
    public int StopsCount { get; set; }
    public int Mttr { get; set; }
    public decimal ProducedOk { get; set; }
    public decimal ProducedNok { get; set; }
}

public class OasParetoEntryDto { public Guid? CauseId { get; set; } public int LostMinutes { get; set; } }

public class OasTrendPointDto { public DateOnly Date { get; set; } public double? Oee { get; set; } }

public class OasLineComparisonEntryDto { public Guid LineId { get; set; } public double Trs { get; set; } public double Scrap { get; set; } public int StopsCount { get; set; } public int MtbfMin { get; set; } }

public class OasSlaSummaryEntryDto { public string EventType { get; set; } = string.Empty; public int Total { get; set; } public int OnTime { get; set; } public double OnTimeRatio { get; set; } }

public class OasCadenceGapEntryDto { public Guid PostId { get; set; } public decimal ActualQty { get; set; } public decimal TheoreticalQty { get; set; } public double GapPercent { get; set; } }

public class OasAndonMessageDto { public Guid? LineId { get; set; } public string Message { get; set; } = string.Empty; }
public class OasAndonMessageRequestDto { public Guid? LineId { get; set; } public string Message { get; set; } = string.Empty; }
