@echo off
setlocal enabledelayedexpansion

echo ====================================================
echo      تثبيت تكامل معالج الضغط مع قائمة النقر الأيمن
echo ====================================================
echo.

:: احصل على المجلد الحالي
set "CURRENT_DIR=%~dp0"
set "APP_PATH=%CURRENT_DIR%app\bin\Debug\app.exe"

:: تحقق من وجود الملف التنفيذي
if not exist "%APP_PATH%" (
    echo خطأ: لم يتم العثور على الملف التنفيذي في المسار التالي:
    echo %APP_PATH%
    echo.
    echo يرجى التأكد من بناء المشروع أولاً.
    pause
    exit /b 1
)

echo جارٍ إنشاء ملف التسجيل...

:: إنشاء ملف التسجيل المؤقت
set "REG_FILE=%TEMP%\compressor_context_menu.reg"

echo Windows Registry Editor Version 5.00 > "%REG_FILE%"
echo. >> "%REG_FILE%"

:: إضافة خيارات الضغط للملفات
echo ; إضافة خيارات الضغط لقائمة النقر الأيمن للملفات >> "%REG_FILE%"
echo [HKEY_CLASSES_ROOT\*\shell\CompressFile] >> "%REG_FILE%"
echo @="Compress File" >> "%REG_FILE%"
echo "Icon"="%APP_PATH:\=\\%,0" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

echo [HKEY_CLASSES_ROOT\*\shell\CompressFile\command] >> "%REG_FILE%"
echo @="\"%APP_PATH:\=\\%\" /compress \"%%1\"" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

:: إضافة خيارات فك الضغط للملفات .huff
echo ; إضافة خيارات فك الضغط للملفات المضغوطة .huff >> "%REG_FILE%"
echo [HKEY_CLASSES_ROOT\.huff] >> "%REG_FILE%"
echo @="HuffmanCompressedFile" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

echo [HKEY_CLASSES_ROOT\HuffmanCompressedFile] >> "%REG_FILE%"
echo @="Huffman Compressed File" >> "%REG_FILE%"
echo "DefaultIcon"="%APP_PATH:\=\\%,0" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

echo [HKEY_CLASSES_ROOT\HuffmanCompressedFile\shell\DecompressFile] >> "%REG_FILE%"
echo @="Extract File" >> "%REG_FILE%"
echo "Icon"="%APP_PATH:\=\\%,0" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

echo [HKEY_CLASSES_ROOT\HuffmanCompressedFile\shell\DecompressFile\command] >> "%REG_FILE%"
echo @="\"%APP_PATH:\=\\%\" /decompress \"%%1\"" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

:: إضافة خيارات فك الضغط للملفات .fs
echo ; إضافة خيارات فك الضغط للملفات المضغوطة .fs >> "%REG_FILE%"
echo [HKEY_CLASSES_ROOT\.fs] >> "%REG_FILE%"
echo @="FanoShannonCompressedFile" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

echo [HKEY_CLASSES_ROOT\FanoShannonCompressedFile] >> "%REG_FILE%"
echo @="Fano-Shannon Compressed File" >> "%REG_FILE%"
echo "DefaultIcon"="%APP_PATH:\=\\%,0" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

echo [HKEY_CLASSES_ROOT\FanoShannonCompressedFile\shell\DecompressFile] >> "%REG_FILE%"
echo @="Extract File" >> "%REG_FILE%"
echo "Icon"="%APP_PATH:\=\\%,0" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

echo [HKEY_CLASSES_ROOT\FanoShannonCompressedFile\shell\DecompressFile\command] >> "%REG_FILE%"
echo @="\"%APP_PATH:\=\\%\" /decompress \"%%1\"" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

:: إضافة خيارات الضغط للمجلدات
echo ; إضافة خيارات الضغط لقائمة النقر الأيمن للمجلدات >> "%REG_FILE%"
echo [HKEY_CLASSES_ROOT\Directory\shell\CompressFolder] >> "%REG_FILE%"
echo @="Compress Folder" >> "%REG_FILE%"
echo "Icon"="%APP_PATH:\=\\%,0" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

echo [HKEY_CLASSES_ROOT\Directory\shell\CompressFolder\command] >> "%REG_FILE%"
echo @="\"%APP_PATH:\=\\%\" /compressfolder \"%%1\"" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

:: إضافة خيار فتح البرنامج من قائمة الخلفية
echo ; إضافة خيار فتح معالج الضغط في قائمة الخلفية >> "%REG_FILE%"
echo [HKEY_CLASSES_ROOT\Directory\Background\shell\OpenCompressor] >> "%REG_FILE%"
echo @="Open File Compressor" >> "%REG_FILE%"
echo "Icon"="%APP_PATH:\=\\%,0" >> "%REG_FILE%"
echo. >> "%REG_FILE%"

echo [HKEY_CLASSES_ROOT\Directory\Background\shell\OpenCompressor\command] >> "%REG_FILE%"
echo @="\"%APP_PATH:\=\\%\"" >> "%REG_FILE%"

echo تم إنشاء ملف التسجيل بنجاح.
echo.

echo جارٍ تطبيق إعدادات التسجيل...
echo تحذير: قد تحتاج إلى صلاحيات المدير لتطبيق هذه التغييرات.
echo.

:: تطبيق ملف التسجيل
regedit.exe /s "%REG_FILE%"

if %ERRORLEVEL% EQU 0 (
    echo تم تثبيت تكامل معالج الضغط بنجاح!
    echo.
    echo يمكنك الآن النقر الأيمن على أي ملف أو مجلد ورؤية خيارات الضغط.
    echo لفك الضغط، انقر الأيمن على الملفات .huff أو .fs
) else (
    echo حدث خطأ أثناء تطبيق إعدادات التسجيل.
    echo يرجى تشغيل هذا الملف كمدير.
)

:: حذف الملف المؤقت
del "%REG_FILE%" 2>nul

echo.
echo لإلغاء تثبيت التكامل، استخدم الملف uninstall_context_menu.bat
echo.
pause 