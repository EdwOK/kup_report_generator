﻿using FluentResults;
using Spectre.Console;

namespace KUPReportGenerator.Generators;

public interface IReportGenerator
{
    Task<Result> Generate(ReportGeneratorContext reportContext, ProgressContext progressContext,
        CancellationToken cancellationToken);
}