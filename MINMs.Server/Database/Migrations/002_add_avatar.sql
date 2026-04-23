ALTER TABLE `users` 
ADD COLUMN `avatar` VARCHAR(255) NULL
AFTER `password_hash`;