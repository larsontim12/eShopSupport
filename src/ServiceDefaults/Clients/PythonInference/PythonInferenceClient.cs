﻿using System.Net.Http.Json;

namespace eShopSupport.ServiceDefaults.Clients.PythonInference;

public class PythonInferenceClient(HttpClient http)
{
    public async Task<string?> ClassifyTextAsync(string text, IEnumerable<string> candidateLabels)
    {
        var response = await http.PostAsJsonAsync("/classify",
            new { text, candidate_labels = candidateLabels });
        var label = await response.Content.ReadFromJsonAsync<string>();
        return label;
    }
}
