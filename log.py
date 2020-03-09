import os
message = input('commit message: ')
cmd0 = 'git commit -m "'+ message +'"'
cmd1 = 'git log --pretty=format:"%h was %an, %ar, message: %s" > log.text'
os.system(cmd0)
os.system(cmd1)
