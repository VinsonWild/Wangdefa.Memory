// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

namespace Wangdefa.Contracts;

public interface IChatService
{
    Task<string> ChatAsync(string prompt);
}