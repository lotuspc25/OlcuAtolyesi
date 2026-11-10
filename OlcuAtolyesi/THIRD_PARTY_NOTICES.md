# Third-Party Notices

Bu dosya Ölçü Atölyesi v0.2.0 içinde dağıtılan veya derleme sırasında kullanılan üçüncü taraf bileşenleri belgeler. Projeye bu bildirimlerde sayılanların dışında üçüncü taraf kod ya da görsel varlık eklenmemiştir.

## Open Sans

Dağıtılan dosyalar:

- `OlcuAtolyesi/Resources/Fonts/OpenSans-Regular.ttf`
- `OlcuAtolyesi/Resources/Fonts/OpenSans-Semibold.ttf`

Copyright 2020 The Open Sans Project Authors (https://github.com/googlefonts/opensans)

Lisans: SIL Open Font License, Version 1.1 (`OFL-1.1`)

### SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007

#### PREAMBLE

The goals of the Open Font License (OFL) are to stimulate worldwide development of collaborative font projects, to support the font creation efforts of academic and linguistic communities, and to provide a free and open framework in which fonts may be shared and improved in partnership with others.

The OFL allows the licensed fonts to be used, studied, modified and redistributed freely as long as they are not sold by themselves. The fonts, including any derivative works, can be bundled, embedded, redistributed and/or sold with any software provided that any reserved names are not used by derivative works. The fonts and derivatives, however, cannot be released under any other type of license. The requirement for fonts to remain under this license does not apply to any document created using the fonts or their derivatives.

#### DEFINITIONS

“Font Software” refers to the set of files released by the Copyright Holder(s) under this license and clearly marked as such. This may include source files, build scripts and documentation.

“Reserved Font Name” refers to any names specified as such after the copyright statement(s).

“Original Version” refers to the collection of Font Software components as distributed by the Copyright Holder(s).

“Modified Version” refers to any derivative made by adding to, deleting, or substituting — in part or in whole — any of the components of the Original Version, by changing formats or by porting the Font Software to a new environment.

“Author” refers to any designer, engineer, programmer, technical writer or other person who contributed to the Font Software.

#### PERMISSION & CONDITIONS

Permission is hereby granted, free of charge, to any person obtaining a copy of the Font Software, to use, study, copy, merge, embed, modify, redistribute, and sell modified and unmodified copies of the Font Software, subject to the following conditions:

1. Neither the Font Software nor any of its individual components, in Original or Modified Versions, may be sold by itself.
2. Original or Modified Versions of the Font Software may be bundled, redistributed and/or sold with any software, provided that each copy contains the above copyright notice and this license. These can be included either as stand-alone text files, human-readable headers or in the appropriate machine-readable metadata fields within text or binary files as long as those fields can be easily viewed by the user.
3. No Modified Version of the Font Software may use the Reserved Font Name(s) unless explicit written permission is granted by the corresponding Copyright Holder. This restriction only applies to the primary font name as presented to the users.
4. The name(s) of the Copyright Holder(s) or the Author(s) of the Font Software shall not be used to promote, endorse or advertise any Modified Version, except to acknowledge the contribution(s) of the Copyright Holder(s) and the Author(s) or with their explicit written permission.
5. The Font Software, modified or unmodified, in part or in whole, must be distributed entirely under this license, and must not be distributed under any other license. The requirement for fonts to remain under this license does not apply to any document created using the Font Software.

#### TERMINATION

This license becomes null and void if any of the above conditions are not met.

#### DISCLAIMER

THE FONT SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM OTHER DEALINGS IN THE FONT SOFTWARE.

## Microsoft .NET ve .NET MAUI paketleri

Proje dosyasında doğrudan başvurulan paketler:

- `Microsoft.Maui.Controls` — sürümü .NET MAUI SDK içindeki `$(MauiVersion)` özelliği belirler; MIT License.
- `Microsoft.Extensions.Logging.Debug` — `10.0.0`; MIT License.

Uygulama ayrıca .NET 10 ve .NET MAUI iş yükünün hedef çerçeve ve çalışma zamanı bileşenlerini kullanır. Her NuGet paketinin kendi paket bildirimi ve bağımlılık lisansı geçerlidir. Kaynak ve lisans bilgileri:

- .NET MAUI: https://github.com/dotnet/maui
- .NET Runtime: https://github.com/dotnet/runtime
- Microsoft.Extensions.Logging.Debug kaynağı: https://github.com/dotnet/runtime/tree/main/src/libraries/Microsoft.Extensions.Logging.Debug
- NuGet paket bilgileri: https://www.nuget.org/

### MIT License

Copyright (c) Microsoft Corporation and Contributors.  
Copyright (c) .NET Foundation and Contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

Microsoft, .NET ve ilgili adlar Microsoft Corporation'ın ticari markaları olabilir. Bu bildirim, Microsoft tarafından onay veya destek verildiği anlamına gelmez.

## Tasarım referansı olarak listelenen GitHub projeleri

Aşağıdaki depolar yalnız yaklaşım araştırması için görev tanımında listelenmiştir:

- `anastr/RulerView`
- `SecUSo/privacy-friendly-ruler`
- `eristime/android-measuring-tool`
- `Qu4X/ReMeasure`
- `streetcomplete/StreetMeasure`

Bu depolardan hiçbir kod, çizim, ikon, fotoğraf veya başka varlık kopyalanmamış ya da projeye uyarlanmamıştır. Eski bağımlılıkları projeye taşınmamıştır. Bu nedenle bu dosyada söz konusu depolara ait bir dağıtım lisansı veya atıf eklenmez.

## Projeye özgü görseller

`OlcuAtolyesi/Resources/Images`, `Resources/AppIcon` ve `Resources/Splash` altındaki ölçüm temalı SVG görseller bu prototip için sıfırdan oluşturulmuştur; dış ikon kitaplığına veya çalışma anında indirilen içeriğe dayanmaz.


## v0.2.0 kamera bağımlılıkları

- CommunityToolkit.Maui.Camera 6.1.0 — MIT; © Microsoft Corporation ve katkıda bulunanlar.
  Kaynak/lisans: https://github.com/CommunityToolkit/Maui — paket: https://www.nuget.org/packages/CommunityToolkit.Maui.Camera/6.1.0
  Gerekçe: uygulama içinde canlı arka kamera, fotoğraf çekimi ve yaşam döngüsü desteği.
  Yalnız Android hedefinde eklenir; genel CommunityToolkit.Maui paketi eklenmemiştir.
- Microsoft.Maui.Controls ve zorunlu MAUI bağımlılıkları 10.0.60 — MIT. Camera paketinin gerekli alt sınırıdır; SDK/iş yükü kurulumu değiştirilmemiştir.
- Kamera paketinin AndroidX/CameraX bağımlılıkları (Camera.Core, Camera.Camera2, Camera.Lifecycle, Camera.View, Camera.Extensions 1.4.2.3 ve onların NuGet ile çözümlenen bağımlılıkları) kullanılır. .NET bağlamaları MIT; AndroidX Java bileşenleri Apache-2.0 ve paket içeriğine göre BSD-3-Clause bildirimleri taşır. Örneğin Camera.Core paket bildirimi: MIT AND Apache-2.0 AND BSD-3-Clause.
  Her bağımlılığın paket içindeki LICENSE.md ve NuGet lisans bildirimi geçerlidir; uygulamaya özgü bir lisans değişikliği yapılmamıştır.

### CameraX bağlamasının dağıtım lisansları
Note: This package may contain or install third-party dependencies which are 
governed by additional license(s). Microsoft is not responsible for, nor 
does it grant any licenses to, third-party packages. See THIRD-PARTY-NOTICES.txt
for any third-party licensing information.

Microsoft bindings included in this package are licensed under The MIT License.

**The MIT License (MIT)**

Copyright (c) .NET Foundation Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.


