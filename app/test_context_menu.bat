@echo off
echo ====================================================
echo         اختبار وظائف سطر الأوامر لمعالج الضغط
echo ====================================================
echo.

:: إنشاء ملف تجريبي
echo هذا ملف تجريبي لاختبار معالج الضغط > test_file.txt
echo يحتوي على نص عربي وإنجليزي >> test_file.txt
echo This is a test file for the compression application >> test_file.txt
echo It contains Arabic and English text >> test_file.txt

echo تم إنشاء ملف تجريبي: test_file.txt
echo.

echo جارٍ اختبار عرض رسالة المساعدة...
echo.

:: اختبار عرض رسالة المساعدة (معاملات خاطئة)
app\bin\Debug\app.exe

echo.
echo.

echo إذا كنت تريد اختبار الضغط عملياً، قم بما يلي:
echo 1. انقر الأيمن على الملف test_file.txt
echo 2. اختر "ضغط الملف باستخدام معالج الضغط"
echo 3. اختر عدم استخدام كلمة سر للاختبار
echo 4. بعد الضغط، انقر الأيمن على test_file.txt.huff
echo 5. اختر "فك ضغط الملف"
echo.

echo ملاحظة: لتفعيل قائمة النقر الأيمن، يجب تشغيل install_context_menu.bat كمدير أولاً
echo.

pause 