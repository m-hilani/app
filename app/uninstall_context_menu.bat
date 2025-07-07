@echo off
setlocal enabledelayedexpansion

echo ====================================================
echo      إلغاء تثبيت تكامل معالج الضغط مع قائمة النقر الأيمن
echo ====================================================
echo.

echo جارٍ إنشاء ملف إلغاء التسجيل...

:: إنشاء ملف إلغاء التسجيل المؤقت
set "REG_FILE=%TEMP%\remove_compressor_context_menu.reg"

echo Windows Registry Editor Version 5.00 > "%REG_FILE%"
echo. >> "%REG_FILE%"

:: حذف خيارات الضغط للملفات
echo ; حذف خيارات الضغط من قائمة النقر الأيمن للملفات >> "%REG_FILE%"
echo [-HKEY_CLASSES_ROOT\*\shell\CompressFile] >> "%REG_FILE%"
echo. >> "%REG_FILE%"

:: حذف خيارات فك الضغط للملفات .huff
echo ; حذف خيارات فك الضغط للملفات المضغوطة .huff >> "%REG_FILE%"
echo [-HKEY_CLASSES_ROOT\.huff] >> "%REG_FILE%"
echo [-HKEY_CLASSES_ROOT\HuffmanCompressedFile] >> "%REG_FILE%"
echo. >> "%REG_FILE%"

:: حذف خيارات فك الضغط للملفات .fs
echo ; حذف خيارات فك الضغط للملفات المضغوطة .fs >> "%REG_FILE%"
echo [-HKEY_CLASSES_ROOT\.fs] >> "%REG_FILE%"
echo [-HKEY_CLASSES_ROOT\FanoShannonCompressedFile] >> "%REG_FILE%"
echo. >> "%REG_FILE%"

:: حذف خيارات الضغط للمجلدات
echo ; حذف خيارات الضغط من قائمة النقر الأيمن للمجلدات >> "%REG_FILE%"
echo [-HKEY_CLASSES_ROOT\Directory\shell\CompressFolder] >> "%REG_FILE%"
echo. >> "%REG_FILE%"

:: حذف خيار فتح البرنامج من قائمة الخلفية
echo ; حذف خيار فتح معالج الضغط من قائمة الخلفية >> "%REG_FILE%"
echo [-HKEY_CLASSES_ROOT\Directory\Background\shell\OpenCompressor] >> "%REG_FILE%"

echo تم إنشاء ملف إلغاء التسجيل بنجاح.
echo.

echo جارٍ تطبيق إعدادات إلغاء التسجيل...
echo تحذير: قد تحتاج إلى صلاحيات المدير لتطبيق هذه التغييرات.
echo.

:: تطبيق ملف إلغاء التسجيل
regedit.exe /s "%REG_FILE%"

if %ERRORLEVEL% EQU 0 (
    echo تم إلغاء تثبيت تكامل معالج الضغط بنجاح!
    echo.
    echo تمت إزالة جميع خيارات الضغط من قائمة النقر الأيمن.
    echo قد تحتاج إلى إعادة تشغيل File Explorer لرؤية التغييرات.
) else (
    echo حدث خطأ أثناء تطبيق إعدادات إلغاء التسجيل.
    echo يرجى تشغيل هذا الملف كمدير.
)

:: حذف الملف المؤقت
del "%REG_FILE%" 2>nul

echo.
echo لإعادة تثبيت التكامل، استخدم الملف install_context_menu.bat
echo.
pause 