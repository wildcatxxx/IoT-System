using System;

namespace TemperatureApi.Models;

public class TemperatureDto
{
    public int Id { get; set; }
    public int Temperature { get; set; }
    public DateTime Time { get; set; }
}